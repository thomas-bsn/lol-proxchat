namespace LoLProximityChat.Core.Interfaces
{
    public interface IRoomService
    {
        Task<string?> JoinOrCreateAsync(string roomId, string playerId, CancellationToken ct);
    }
}