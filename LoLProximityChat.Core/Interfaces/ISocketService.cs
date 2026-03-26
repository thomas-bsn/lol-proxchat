using LoLProximityChat.Shared.DTOs;

namespace LoLProximityChat.Core.Interfaces
{
    public interface ISocketService
    {
        event Action?                    OnDisconnected;
        event Action<VolumePayload>?     OnVolumePayloadReceived;

        Task ConnectAsync(string token, CancellationToken ct);
        Task SendAsync(PositionPayload payload, CancellationToken ct = default);
        Task ReconnectAsync(CancellationToken ct);
        Task DisconnectAsync();
    }
}