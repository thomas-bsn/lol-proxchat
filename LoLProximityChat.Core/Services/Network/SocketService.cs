using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using LoLProximityChat.Core.Interfaces;
using LoLProximityChat.Core.Models;
using LoLProximityChat.Shared.DTOs;

namespace LoLProximityChat.Core.Services.Network
{
    public class SocketService : ISocketService
    {
        private ClientWebSocket? _ws;
        private readonly AppConfig _config;
        private string? _token;

        public event Action?                        OnDisconnected;
        public event Action<VolumePayload>?         OnVolumePayloadReceived;

        public SocketService(AppConfig config)
        {
            _config = config;
        }

        public async Task ConnectAsync(string token, CancellationToken ct)
        {
            _token = token;
            _ws    = new ClientWebSocket();

            var uri = new Uri($"{_config.ServerUrl.Replace("https", "wss").Replace("http", "ws")}/room/ws?token={token}");
            await _ws.ConnectAsync(uri, ct);

            _ = ReceiveLoopAsync(ct);
        }

        public async Task SendAsync(PositionPayload payload, CancellationToken ct = default)
        {
            if (_ws?.State != WebSocketState.Open) return;

            var json  = JsonSerializer.Serialize(payload);
            var bytes = Encoding.UTF8.GetBytes(json);
            await _ws.SendAsync(bytes, WebSocketMessageType.Text, true, ct);
        }

        public async Task ReconnectAsync(CancellationToken ct)
        {
            await DisconnectAsync();
            await ConnectAsync(_token!, ct);
        }

        public async Task DisconnectAsync()
        {
            if (_ws is null) return;

            if (_ws.State == WebSocketState.Open)
                await _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Déconnexion", CancellationToken.None);

            _ws.Dispose();
            _ws = null;
        }

        private async Task ReceiveLoopAsync(CancellationToken ct)
        {
            var buffer = new byte[4096];

            try
            {
                while (_ws?.State == WebSocketState.Open && !ct.IsCancellationRequested)
                {
                    var result = await _ws.ReceiveAsync(buffer, ct);

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        OnDisconnected?.Invoke();
                        return;
                    }

                    var json    = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    var payload = JsonSerializer.Deserialize<VolumePayload>(json);

                    if (payload is not null)
                        OnVolumePayloadReceived?.Invoke(payload);
                }
            }
            catch (Exception)
            {
                OnDisconnected?.Invoke();
            }
        }
    }
}