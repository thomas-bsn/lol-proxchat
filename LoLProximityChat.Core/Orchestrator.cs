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
        public OrchestratorState State => _state;
        public event Action<OrchestratorState>? OnStateChanged;

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
        
        private void SetState(OrchestratorState newState)
        {
            if (_state == newState) return;

            _state = newState;
            OnStateChanged?.Invoke(_state);
        }

        public async Task<bool> JoinAndConnectAsync(string roomId, string playerId, CancellationToken ct)
        {
            if (_state != OrchestratorState.Idle) return false;

            SetState(OrchestratorState.Disconnected); // → PENDING

            var token = await WaitForServerAsync(roomId, playerId, ct);
            if (token is null) return false;

            var discordOk = await _discordRpcService.ConnectAsync(ct);
            if (!discordOk)
            {
                SetState(OrchestratorState.Idle);
                return false;
            }

            try
            {
                await _socketService.ConnectAsync(token, ct);
                SetState(OrchestratorState.InGame);
                return true;
            }
            catch
            {
                SetState(OrchestratorState.Disconnected);
                return false;
            }
        }
        
        private async Task<string?> WaitForServerAsync(string roomId, string playerId, CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                var token = await _roomService.JoinOrCreateAsync(roomId, playerId, ct);

                if (token != null)
                    return token;

                await Task.Delay(2000, ct);
            }

            return null;
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
            SetState(OrchestratorState.Disconnected);

            var reconnected = await _reconnectionPolicy.TryReconnectAsync(
                () => _socketService.ReconnectAsync(CancellationToken.None),
                CancellationToken.None
            );

            SetState(reconnected ? OrchestratorState.InGame : OrchestratorState.Idle);
        }

        // --- Arrêt ---

        public async Task StopAsync()
        {
            await _discordRpcService.ResetAsync();
            await _socketService.DisconnectAsync();

            SetState(OrchestratorState.Idle);
            _lastSentPosition = null;
        }
    }
}