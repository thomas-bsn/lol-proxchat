using System.IO.Pipes;
using System.Text;
using System.Text.Json;

namespace LoLProximityChat.Core.Audio
{
    public class DiscordRpcService : IDisposable
    {
        public Dictionary<string, string> VoiceMembers { get; private set; } = new();
        public event Action<Dictionary<string, string>>? OnVoiceMembersChanged;

        private readonly string _clientId;
        private readonly string _serverUrl;
        private NamedPipeClientStream? _pipe;
        private CancellationTokenSource? _cts;
        private readonly SemaphoreSlim _writeLock = new(1, 1);
        private bool _authorized = false;
        private string? _currentChannelId = null;

        public DiscordRpcService(string clientId, string serverUrl)
        {
            _clientId  = clientId;
            _serverUrl = serverUrl;
        }

        public async Task ConnectAsync()
        {
            _cts = new CancellationTokenSource();

            for (int i = 0; i <= 9; i++)
            {
                try
                {
                    _pipe = new NamedPipeClientStream(".", $"discord-ipc-{i}",
                        PipeDirection.InOut, PipeOptions.Asynchronous);
                    await _pipe.ConnectAsync(1000);
                    Console.WriteLine($"[DISCORD RPC] Connecté sur discord-ipc-{i}");
                    break;
                }
                catch
                {
                    _pipe?.Dispose();
                    _pipe = null;
                }
            }

            if (_pipe == null)
            {
                Console.WriteLine("[DISCORD RPC] Discord non trouvé");
                return;
            }

            await HandshakeAsync();
            _ = ReadLoopAsync(_cts.Token);
        }

        private async Task HandshakeAsync()
            => await WriteFrameAsync(0, new { v = 1, client_id = _clientId });

        private async Task AuthorizeAsync()
            => await WriteFrameAsync(1, new
            {
                cmd  = "AUTHORIZE",
                args = new { client_id = _clientId, scopes = new[] { "rpc", "rpc.voice.read" } },
                nonce = Guid.NewGuid().ToString()
            });

        private async Task AuthenticateAsync(string accessToken)
            => await WriteFrameAsync(1, new
            {
                cmd  = "AUTHENTICATE",
                args = new { access_token = accessToken },
                nonce = Guid.NewGuid().ToString()
            });

        private async Task GetVoiceChannelAsync()
        {
            if (!_authorized) return;
            await WriteFrameAsync(1, new
            {
                cmd   = "GET_SELECTED_VOICE_CHANNEL",
                args  = new { },
                nonce = Guid.NewGuid().ToString()
            });
        }

        // Subscribe aux events d'un channel spécifique
        private async Task SubscribeToChannelAsync(string channelId)
        {
            foreach (var evt in new[] { "VOICE_STATE_CREATE", "VOICE_STATE_UPDATE", "VOICE_STATE_DELETE" })
            {
                await WriteFrameAsync(1, new
                {
                    cmd  = "SUBSCRIBE",
                    evt,
                    args = new { channel_id = channelId },
                    nonce = Guid.NewGuid().ToString()
                });
            }
            Console.WriteLine($"[DISCORD RPC] Subscribed à channel {channelId}");
        }

        // Unsubscribe de l'ancien channel avant d'en rejoindre un nouveau
        private async Task UnsubscribeFromChannelAsync(string channelId)
        {
            foreach (var evt in new[] { "VOICE_STATE_CREATE", "VOICE_STATE_UPDATE", "VOICE_STATE_DELETE" })
            {
                await WriteFrameAsync(1, new
                {
                    cmd  = "UNSUBSCRIBE",
                    evt,
                    args = new { channel_id = channelId },
                    nonce = Guid.NewGuid().ToString()
                });
            }
        }

        // Subscribe à VOICE_CHANNEL_SELECT — pas besoin de channelId
        private async Task SubscribeToChannelSelectAsync()
            => await WriteFrameAsync(1, new
            {
                cmd   = "SUBSCRIBE",
                evt   = "VOICE_CHANNEL_SELECT",
                nonce = Guid.NewGuid().ToString()
            });

        public async Task SetUserVolumeAsync(string userId, float volume)
        {
            if (!_authorized) return;
            int discordVolume = (int)Math.Clamp(volume * 100f, 0, 100);
            await WriteFrameAsync(1, new
            {
                cmd  = "SET_USER_VOICE_SETTINGS",
                args = new { user_id = userId, volume = discordVolume },
                nonce = Guid.NewGuid().ToString()
            });
            Console.WriteLine($"[DISCORD RPC] Volume {userId} → {discordVolume}");
        }

        private async Task ReadLoopAsync(CancellationToken ct)
        {
            var header = new byte[8];
            while (!ct.IsCancellationRequested && _pipe?.IsConnected == true)
            {
                try
                {
                    int read = 0;
                    while (read < 8)
                        read += await _pipe.ReadAsync(header, read, 8 - read, ct);

                    int opcode = BitConverter.ToInt32(header, 0);
                    int length = BitConverter.ToInt32(header, 4);

                    var body = new byte[length];
                    read = 0;
                    while (read < length)
                        read += await _pipe.ReadAsync(body, read, length - read, ct);

                    var json = Encoding.UTF8.GetString(body);
                    Console.WriteLine($"[DISCORD RPC] ← {json[..Math.Min(json.Length, 200)]}");
                    await HandleMessageAsync(json);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[DISCORD RPC] Read error: {ex.Message}");
                    break;
                }
            }
        }

        private async Task HandleMessageAsync(string json)
        {
            try
            {
                var doc    = JsonDocument.Parse(json);
                var root   = doc.RootElement;

                if (!root.TryGetProperty("cmd", out var cmd)) return;
                var cmdStr = cmd.GetString();

                // READY → demande autorisation
                if (cmdStr == "DISPATCH" &&
                    root.TryGetProperty("evt", out var evt) &&
                    evt.GetString() == "READY")
                {
                    Console.WriteLine("[DISCORD RPC] Ready → autorisation...");
                    await AuthorizeAsync();
                    return;
                }

                // AUTHORIZE → échange code contre token
                if (cmdStr == "AUTHORIZE")
                {
                    if (root.TryGetProperty("evt", out var authEvt) &&
                        authEvt.GetString() == "ERROR")
                    {
                        Console.WriteLine("[DISCORD RPC] Autorisation refusée");
                        return;
                    }

                    var code = root.GetProperty("data").GetProperty("code").GetString();
                    if (code is null) return;

                    Console.WriteLine("[DISCORD RPC] Code reçu → échange token...");
                    var token = await ExchangeCodeAsync(code);
                    if (token is null) return;

                    await AuthenticateAsync(token);
                    return;
                }

                // AUTHENTICATE → on est authentifié
                if (cmdStr == "AUTHENTICATE")
                {
                    if (root.TryGetProperty("evt", out var authEvt2) &&
                        authEvt2.GetString() == "ERROR")
                    {
                        Console.WriteLine("[DISCORD RPC] Authentification échouée");
                        return;
                    }

                    Console.WriteLine("[DISCORD RPC] Authentifié ✓");
                    _authorized = true;

                    // Subscribe à VOICE_CHANNEL_SELECT pour détecter les changements de channel
                    await SubscribeToChannelSelectAsync();
                    // Récupère le channel actuel
                    await GetVoiceChannelAsync();
                    return;
                }

                if (!_authorized) return;

                // Réponse GET_SELECTED_VOICE_CHANNEL
                if (cmdStr == "GET_SELECTED_VOICE_CHANNEL")
                {
                    if (!root.TryGetProperty("data", out var data)) return;

                    if (data.ValueKind == JsonValueKind.Null)
                    {
                        Console.WriteLine("[DISCORD RPC] Pas dans un channel vocal");
                        VoiceMembers = new();
                        OnVoiceMembersChanged?.Invoke(VoiceMembers);
                        return;
                    }

                    var channelId = data.GetProperty("id").GetString()!;

                    // Si on change de channel, unsubscribe de l'ancien
                    if (_currentChannelId != null && _currentChannelId != channelId)
                        await UnsubscribeFromChannelAsync(_currentChannelId);

                    // Subscribe au nouveau channel si différent
                    if (_currentChannelId != channelId)
                    {
                        _currentChannelId = channelId;
                        await SubscribeToChannelAsync(channelId);
                    }

                    ParseVoiceMembers(data);
                    return;
                }

                // VOICE_CHANNEL_SELECT — l'utilisateur a changé de channel
                if (cmdStr == "DISPATCH" &&
                    root.TryGetProperty("evt", out var evt2) &&
                    evt2.GetString() == "VOICE_CHANNEL_SELECT")
                {
                    Console.WriteLine("[DISCORD RPC] Changement de channel → refresh...");
                    await GetVoiceChannelAsync();
                    return;
                }

                // Events voice state dans le channel actuel
                if (cmdStr == "DISPATCH" &&
                    root.TryGetProperty("evt", out var evt3))
                {
                    var evtName = evt3.GetString();
                    if (evtName is "VOICE_STATE_CREATE" or "VOICE_STATE_UPDATE" or "VOICE_STATE_DELETE")
                        await GetVoiceChannelAsync();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DISCORD RPC] Parse error: {ex.Message}");
            }
        }

        private async Task<string?> ExchangeCodeAsync(string code)
        {
            try
            {
                using var http = new System.Net.Http.HttpClient();
                var payload    = JsonSerializer.Serialize(new { code });
                var content    = new System.Net.Http.StringContent(
                    payload, Encoding.UTF8, "application/json");
                var response   = await http.PostAsync(
                    $"{_serverUrl}/auth/discord/token", content);

                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"[DISCORD RPC] Échange token échoué: {response.StatusCode}");
                    return null;
                }

                var json  = await response.Content.ReadAsStringAsync();
                var doc   = JsonDocument.Parse(json);
                var token = doc.RootElement.GetProperty("access_token").GetString();
                Console.WriteLine("[DISCORD RPC] Token obtenu ✓");
                return token;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DISCORD RPC] ExchangeCode erreur: {ex.Message}");
                return null;
            }
        }

        private void ParseVoiceMembers(JsonElement channelData)
        {
            var members = new Dictionary<string, string>();

            if (channelData.TryGetProperty("voice_states", out var states))
            {
                foreach (var state in states.EnumerateArray())
                {
                    if (!state.TryGetProperty("user", out var user)) continue;
                    var id       = user.GetProperty("id").GetString()       ?? "";
                    var username = user.GetProperty("username").GetString() ?? "";
                    if (id != "" && username != "")
                        members[username] = id;
                }
            }

            VoiceMembers = members;
            OnVoiceMembersChanged?.Invoke(members);
            Console.WriteLine($"[DISCORD RPC] {members.Count} membres : {string.Join(", ", members.Keys)}");
        }

        private async Task WriteFrameAsync(int opcode, object payload)
        {
            if (_pipe == null || !_pipe.IsConnected) return;

            var body   = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload));
            var header = new byte[8];
            BitConverter.GetBytes(opcode).CopyTo(header, 0);
            BitConverter.GetBytes(body.Length).CopyTo(header, 4);

            await _writeLock.WaitAsync();
            try
            {
                await _pipe.WriteAsync(header);
                await _pipe.WriteAsync(body);
                await _pipe.FlushAsync();
            }
            finally
            {
                _writeLock.Release();
            }
        }

        public void Dispose()
        {
            _cts?.Cancel();
            _pipe?.Dispose();
            _writeLock.Dispose();
        }
    }
}