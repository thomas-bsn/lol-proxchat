using System.Text.Json.Serialization;

namespace LoLProximityChat.Core.Models
{
    public class PlayerPosition
    {
        // Vient de l'API Riot
        [JsonPropertyName("summonerName")]
        public string SummonerName  { get; set; } = "";

        [JsonPropertyName("championName")]
        public string ChampionName  { get; set; } = "";

        [JsonPropertyName("team")]
        public string Team          { get; set; } = "";

        [JsonPropertyName("isDead")]
        public bool   IsDead        { get; set; }

        public bool   IsLocalPlayer { get; set; }

        // Vient du CV pipeline
        public float  X             { get; set; }
        public float  Y             { get; set; }
        public bool   IsVisible     { get; set; }

        public string TeamLabel => Team == "ORDER" ? "Bleu" : "Rouge";
    }
}