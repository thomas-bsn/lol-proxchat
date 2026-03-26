using System.Text.Json.Serialization;

namespace LoLProximityChat.Shared.DTOs
{
    public record PositionPayload(
        [property: JsonPropertyName("playerId")] string PlayerId,
        [property: JsonPropertyName("x")]        float X,
        [property: JsonPropertyName("y")]        float Y
    );
}