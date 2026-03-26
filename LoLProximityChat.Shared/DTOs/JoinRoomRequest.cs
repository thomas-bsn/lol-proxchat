namespace LoLProximityChat.Shared.DTOs
{
    public record JoinRoomRequest(string RoomId, string PlayerId, string DiscordUsername);
}