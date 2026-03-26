namespace LoLProximityChat.Shared.DTOs
{
    // Mapping de tous les joueurs → volume attribué pour CE client
    public record PlayerVolume(float Volume, string DiscordUsername);
    public record VolumePayload(Dictionary<string, PlayerVolume> Volumes);
}