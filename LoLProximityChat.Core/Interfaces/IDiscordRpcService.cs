using LoLProximityChat.Shared.DTOs;

public interface IDiscordRpcService
{
    // IDiscordRpcService
    Task<bool> ConnectAsync(CancellationToken ct = default);
    Task ApplyVolumesAsync(VolumePayload payload);
    Task ResetAsync();
    void Dispose();
}