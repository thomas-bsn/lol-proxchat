using System.Text.Json.Serialization;

namespace LoLProximityChat.Core.Models
{
    public class PlayerInfo
    {
        [JsonPropertyName("summonerName")]
        public string SummonerName { get; set; } = "";

        [JsonPropertyName("championName")]
        public string ChampionName { get; set; } = "";

        [JsonPropertyName("team")]
        public string Team { get; set; } = ""; // "ORDER" | "CHAOS"

        [JsonPropertyName("isDead")]
        public bool IsDead { get; set; }

        [JsonPropertyName("position")]
        public string Position { get; set; } = ""; // TOP | JUNGLE | MIDDLE | BOTTOM | UTILITY

        // Renseigné localement après désérialisation
        public bool IsLocalPlayer { get; set; }

        public string TeamLabel => Team == "ORDER" ? "Bleu" : "Rouge";
    }
}