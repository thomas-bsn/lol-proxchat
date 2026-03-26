using LoLProximityChat.Core.Core;
using LoLProximityChat.Core.Interfaces;
using LoLProximityChat.Core.Models;
using LoLProximityChat.Core.Services.Network;
using LoLProximityChat.Shared.Constants;
using LoLProximityChat.Shared.DTOs;

namespace LoLProximityChat.Core
{
    public enum OrchestratorState { Idle, InGame, Disconnected }

    public class Orchestrator
    {
        private readonly RoomService        _roomService;
        private readonly SocketService      _socketService;
        private readonly ReconnectionPolicy _reconnectionPolicy;
        private readonly IDiscordRpcService _discordRpcService;

        private OrchestratorState _state = OrchestratorState.Idle;
        private PlayerPosition?   _lastSentPosition;

        public Orchestrator(
            RoomService         roomService,
            SocketService       socketService,
            ReconnectionPolicy  reconnectionPolicy,
            IDiscordRpcService  discordRpcService)
        {
            _roomService        = roomService;
            _socketService      = socketService;
            _reconnectionPolicy = reconnectionPolicy;
            _discordRpcService  = discordRpcService;

            _socketService.OnDisconnected          += () => _ = OnDisconnectedAsync();
            _socketService.OnVolumePayloadReceived += OnVolumePayloadReceived;
        }

        // --- Démarrage ---

        public async Task<bool> JoinAndConnectAsync(string roomId, string playerId, CancellationToken ct)
        {
            if (_state != OrchestratorState.Idle) return false;

            var discordConnected = await _discordRpcService.ConnectAsync(ct);
            if (!discordConnected) return false;

            var token = await _roomService.JoinOrCreateAsync(roomId, playerId, ct);
            await _socketService.ConnectAsync(token, ct);

            _state = OrchestratorState.InGame;
            return true;
        }

        // --- Envoi position ---

        public async Task SendPositionAsync(PlayerPosition position, CancellationToken ct = default)
        {
            if (_state != OrchestratorState.InGame) return;

            if (_lastSentPosition is not null)
            {
                var dx = position.X - _lastSentPosition.X;
                var dy = position.Y - _lastSentPosition.Y;
                if (Math.Sqrt(dx * dx + dy * dy) < ProximityConstants.MovementThreshold)
                    return;
            }

            _lastSentPosition = position;

            await _socketService.SendAsync(new PositionPayload(
                position.SummonerName,
                position.X,
                position.Y
            ), ct);
        }

        // --- Réception volumes ---

        private void OnVolumePayloadReceived(VolumePayload payload)
        {
            if (_state != OrchestratorState.InGame) return;
            _ = _discordRpcService.ApplyVolumesAsync(payload);
        }

        // --- Déconnexion ---

        private async Task OnDisconnectedAsync()
        {
            _state = OrchestratorState.Disconnected;

            var reconnected = await _reconnectionPolicy.TryReconnectAsync(
                () => _socketService.ReconnectAsync(CancellationToken.None),
                CancellationToken.None
            );

            _state = reconnected ? OrchestratorState.InGame : OrchestratorState.Idle;
        }

        // --- Arrêt ---

        public async Task StopAsync()
        {
            await _discordRpcService.ResetAsync();
            await _socketService.DisconnectAsync();

            _state            = OrchestratorState.Idle;
            _lastSentPosition = null;
        }
    }
}