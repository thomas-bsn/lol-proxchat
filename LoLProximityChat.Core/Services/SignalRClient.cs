using Microsoft.AspNetCore.SignalR.Client;

namespace LoLProximityChat.Core.Services
{
    public class SignalRClient : IAsyncDisposable
    {
        private HubConnection? _connection;
        private readonly string _serverUrl;

        public event Action<string>? OnPlayerJoined;
        public event Action<string>? OnPlayerLeft;
        public event Action<string, float, float>? OnPositionUpdated;
        public event Action<string, Dictionary<string, float>>? OnVolumesUpdated;


        public SignalRClient(string serverUrl)
        {
            _serverUrl = serverUrl;
        }

        public async Task ConnectAsync()
        {
            _connection = new HubConnectionBuilder()
                .WithUrl($"{_serverUrl}/proximity")
                .WithAutomaticReconnect()
                .Build();

            _connection.On<string>("PlayerJoined", name => OnPlayerJoined?.Invoke(name));
            _connection.On<string>("PlayerLeft",   name => OnPlayerLeft?.Invoke(name));
            _connection.On<string, float, float>("PositionUpdated", (name, x, y) =>
                OnPositionUpdated?.Invoke(name, x, y));
            _connection.On<string, Dictionary<string, float>>("VolumesUpdated",
                (listenerName, volumes) => OnVolumesUpdated?.Invoke(listenerName, volumes));

            try
            {
                await _connection.StartAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SignalR] Connexion échouée : {ex.Message}");
            }
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