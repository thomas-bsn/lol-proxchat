using Microsoft.AspNetCore.SignalR.Client;

namespace LoLProximityChat.Core.Services
{
    public class SignalRClient : IAsyncDisposable
    {
        private HubConnection? _connection;
        private readonly string _serverUrl;

        public event Action<string>?                    OnPlayerJoined;
        public event Action<string>?                    OnPlayerLeft;
        public event Action<Dictionary<string, float>>? OnVolumesUpdated;
        public event Action<string, byte[]>?            OnAudioReceived;
        public event Action<bool>?                      OnConnectionChanged;
        public event Action<string, string, int>?       OnPeerEndpoint;
        public event Action<List<string>>?              OnExistingPlayers; // NOUVEAU

        public SignalRClient(string serverUrl)
        {
            _serverUrl = serverUrl;
        }

        public async Task ConnectAsync()
        {
            if (_connection is not null)
                await _connection.DisposeAsync();

            _connection = new HubConnectionBuilder()
                .WithUrl($"{_serverUrl}/proximity")
                .WithAutomaticReconnect(new[] { 0, 2000, 5000, 10000, 30000 }
                    .Select(ms => TimeSpan.FromMilliseconds(ms)).ToArray())
                .Build();

            _connection.On<string>("PlayerJoined",
                name => OnPlayerJoined?.Invoke(name));
            _connection.On<string>("PlayerLeft",
                name => OnPlayerLeft?.Invoke(name));
            _connection.On<Dictionary<string, float>>("VolumesUpdated",
                volumes => OnVolumesUpdated?.Invoke(volumes));
            _connection.On<string, string, int>("PeerEndpoint",
                (name, ip, port) => OnPeerEndpoint?.Invoke(name, ip, port));
            _connection.On<string, byte[]>("ReceiveAudio",
                (name, data) => OnAudioReceived?.Invoke(name, data));
            _connection.On<List<string>>("ExistingPlayers",          // NOUVEAU
                names => OnExistingPlayers?.Invoke(names));

            _connection.Reconnected  += _ => { OnConnectionChanged?.Invoke(true);  return Task.CompletedTask; };
            _connection.Reconnecting += _ => { OnConnectionChanged?.Invoke(false); return Task.CompletedTask; };
            _connection.Closed       += _ => { OnConnectionChanged?.Invoke(false); return Task.CompletedTask; };

            for (int i = 0; i < 5; i++)
            {
                try
                {
                    await _connection.StartAsync();
                    OnConnectionChanged?.Invoke(true);
                    return;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[SignalR] Tentative {i + 1}/5 échouée : {ex.Message}");
                    OnConnectionChanged?.Invoke(false);
                    if (i < 4)
                        await Task.Delay(2000 * (i + 1));
                }
            }

            Console.WriteLine("[SignalR] Impossible de se connecter après 5 tentatives.");
        }

        public async Task SendAudioAsync(string gameId, string playerName, byte[] data)
        {
            if (_connection is null || _connection.State != HubConnectionState.Connected) return;
            await _connection.InvokeAsync("SendAudio", gameId, playerName, data);
        }

        public async Task RegisterEndpointAsync(string gameId, string playerName, string ip, int port)
        {
            if (_connection is null || _connection.State != HubConnectionState.Connected) return;
            await _connection.InvokeAsync("RegisterEndpoint", gameId, playerName, ip, port);
        }

        public async Task JoinGameAsync(string gameId, string playerName)
        {
            if (_connection is null || _connection.State != HubConnectionState.Connected) return;
            await _connection.InvokeAsync("JoinGame", gameId, playerName);
        }

        public async Task UpdatePositionAsync(string gameId, string playerName, float x, float y)
        {
            if (_connection is null || _connection.State != HubConnectionState.Connected) return;
            await _connection.InvokeAsync("UpdatePosition", gameId, playerName, x, y);
        }

        public async Task LeaveGameAsync(string gameId, string playerName)
        {
            if (_connection is null || _connection.State != HubConnectionState.Connected) return;
            await _connection.InvokeAsync("LeaveGame", gameId, playerName);
        }

        public async ValueTask DisposeAsync()
        {
            if (_connection is not null)
                await _connection.DisposeAsync();
        }
    }
}