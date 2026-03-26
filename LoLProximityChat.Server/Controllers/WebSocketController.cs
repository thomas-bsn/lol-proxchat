using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using LoLProximityChat.Server.Services;
using LoLProximityChat.Shared.DTOs;

namespace LoLProximityChat.Server.Controllers
{
    public class WebSocketController
    {
        private readonly RoomService                  _roomService;
        private readonly ProximityService             _proximityService;
        private readonly WsConnectionManager          _wsManager;
        private readonly ILogger<WebSocketController> _logger;

        public WebSocketController(
            RoomService                  roomService,
            ProximityService             proximityService,
            WsConnectionManager          wsManager,
            ILogger<WebSocketController> logger)
        {
            _roomService      = roomService;
            _proximityService = proximityService;
            _wsManager        = wsManager;
            _logger           = logger;
        }

        public async Task HandleAsync(HttpContext context)
        {
            if (!context.WebSockets.IsWebSocketRequest)
            {
                context.Response.StatusCode = 400;
                return;
            }

            var token      = context.Request.Query["token"].ToString();
            var playerName = _roomService.GetPlayerName(token);
            var gameId     = _roomService.GetGameId(token);

            if (playerName is null || gameId is null)
            {
                context.Response.StatusCode = 401;
                return;
            }

            var ws = await context.WebSockets.AcceptWebSocketAsync();
            _wsManager.Add(token, ws);
            _roomService.ActivatePlayer(token, playerName, gameId);

            _logger.LogInformation("[WS] {Player} connecté à la room {GameId}", playerName, gameId);

            await HandleClientAsync(ws, token, playerName, gameId);
        }

        private async Task HandleClientAsync(
            WebSocket ws,
            string    connectionId,
            string    playerName,
            string    gameId)
        {
            var buffer = new byte[4096];

            try
            {
                while (ws.State == WebSocketState.Open)
                {
                    var result = await ws.ReceiveAsync(buffer, CancellationToken.None);

                    if (result.MessageType == WebSocketMessageType.Close)
                        break;

                    var json    = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    var payload = JsonSerializer.Deserialize<PositionPayload>(json);

                    if (payload is null) continue;

                    _roomService.UpdatePosition(gameId, playerName, payload.X, payload.Y);

                    var volumes = _proximityService.ComputeVolumes(gameId, _roomService);

                    foreach (var (_, (targetConnId, volumePayload)) in volumes)
                    {
                        var targetWs = _wsManager.GetWebSocket(targetConnId);
                        if (targetWs?.State != WebSocketState.Open) continue;

                        var responseBytes = Encoding.UTF8.GetBytes(
                            JsonSerializer.Serialize(volumePayload)
                        );

                        await targetWs.SendAsync(
                            responseBytes,
                            WebSocketMessageType.Text,
                            true,
                            CancellationToken.None
                        );
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("[WS] Erreur pour {Player} : {Error}", playerName, ex.Message);
            }
            finally
            {
                _wsManager.Remove(connectionId);
                _roomService.RemovePlayer(connectionId);

                _logger.LogInformation("[WS] {Player} déconnecté", playerName);

                if (ws.State == WebSocketState.Open)
                    await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Déconnexion", CancellationToken.None);

                ws.Dispose();
            }
        }
    }
}