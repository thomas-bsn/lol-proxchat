namespace LoLProximityChat.Core.Models
{
    public class AppConfig
    {
        public int    MinimapX           { get; set; }
        public int    MinimapY           { get; set; }
        public int    MinimapSize        { get; set; }
        public string ServerUrl          { get; set; } = "";
        public string DiscordUsername    { get; set; } = "";
        public string DiscordRedirectUri { get; set; } = "";
        public string DiscordClientId    { get; set; } = "";
    }
}