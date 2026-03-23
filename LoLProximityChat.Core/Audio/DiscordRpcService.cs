using System.IO.Pipes;
using System.Text;
using System.Text.Json;

namespace LoLProximityChat.Core.Audio
{
    public class DiscordRpcService : IDisposable
    {
        // username Discord → user_id Discord
        public Dictionary<string, string> VoiceMembers { get; private set; } = new();
        public event Action<Dictionary<string, string>>? OnVoiceMembersChanged;

        private readonly string _clientId;
        private NamedPipeClientStream? _pipe;
        private CancellationTokenSource? _cts;
        private readonly SemaphoreSlim _writeLock = new(1, 1);

        public DiscordRpcService(string clientId)
        {
            _clientId = clientId;
        }

        // ── Connexion ─────────────────────────────────────────────────────────
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
                Console.WriteLine("[DISCORD RPC] Discord non trouvé — Discord est-il ouvert ?");
                return;
            }

            await HandshakeAsync();
            _ = ReadLoopAsync(_cts.Token);
        }

        // ── Handshake ─────────────────────────────────────────────────────────
        private async Task HandshakeAsync()
        {
            await WriteFrameAsync(0, new { v = 1, client_id = _clientId });
        }

        // ── Volume par utilisateur ────────────────────────────────────────────
        public async Task SetUserVolumeAsync(string userId, float volume)
        {
            // volume 0.0-1.0 → Discord attend 0-200 (100 = normal)
            int discordVolume = (int)Math.Clamp(volume * 200f, 0, 200);

            await WriteFrameAsync(1, new
            {
                cmd   = "SET_USER_VOICE_SETTINGS",
                args  = new { user_id = userId, volume = discordVolume },
                nonce = Guid.NewGuid().ToString()
            });

            Console.WriteLine($"[DISCORD RPC] Volume {userId} → {discordVolume}");
        }

        // ── Récupère les membres du channel vocal ─────────────────────────────
        private async Task GetVoiceChannelAsync()
        {
            await WriteFrameAsync(1, new
            {
                cmd   = "GET_SELECTED_VOICE_CHANNEL",
                args  = new { },
                nonce = Guid.NewGuid().ToString()
            });
        }

        private async Task SubscribeVoiceStateAsync()
        {
            await WriteFrameAsync(1, new
            {
                cmd   = "SUBSCRIBE",
                evt   = "VOICE_STATE_CREATE",
                nonce = Guid.NewGuid().ToString()
            });
            await WriteFrameAsync(1, new
            {
                cmd   = "SUBSCRIBE",
                evt   = "VOICE_STATE_UPDATE",
                nonce = Guid.NewGuid().ToString()
            });
            await WriteFrameAsync(1, new
            {
                cmd   = "SUBSCRIBE",
                evt   = "VOICE_STATE_DELETE",
                nonce = Guid.NewGuid().ToString()
            });
        }

        // ── Read loop ─────────────────────────────────────────────────────────
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
                var doc  = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (!root.TryGetProperty("cmd", out var cmd)) return;
                var cmdStr = cmd.GetString();

                // READY — handshake ok, on subscribe aux events
                if (cmdStr == "DISPATCH" &&
                    root.TryGetProperty("evt", out var evt) &&
                    evt.GetString() == "READY")
                {
                    Console.WriteLine("[DISCORD RPC] Ready");
                    await SubscribeVoiceStateAsync();
                    await GetVoiceChannelAsync();
                    return;
                }

                // Réponse GET_SELECTED_VOICE_CHANNEL
                if (cmdStr == "GET_SELECTED_VOICE_CHANNEL")
                {
                    if (root.TryGetProperty("data", out var data) &&
                        data.ValueKind != JsonValueKind.Null)
                        ParseVoiceMembers(data);
                    return;
                }

                // Events voice state
                if (cmdStr == "DISPATCH" &&
                    root.TryGetProperty("evt", out var evt2))
                {
                    var evtName = evt2.GetString();
                    if (evtName is "VOICE_STATE_CREATE" or "VOICE_STATE_UPDATE" or "VOICE_STATE_DELETE")
                        await GetVoiceChannelAsync();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DISCORD RPC] Parse error: {ex.Message}");
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

        // ── Write ─────────────────────────────────────────────────────────────
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