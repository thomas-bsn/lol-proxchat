using System.Text.Json.Serialization;

namespace LoLProximityChat.Core.Models
{
    public class GameState
    {
        public bool   IsInGame        { get; set; }
        public string LocalPlayerName { get; set; } = "";
        public float  GameTime        { get; set; }
        public List<PlayerPosition> Players { get; set; } = [];

        public List<PlayerPosition> OrderTeam   => Players.Where(p => p.Team == "ORDER").ToList();
        public List<PlayerPosition> ChaosTeam   => Players.Where(p => p.Team == "CHAOS").ToList();
        public PlayerPosition?      LocalPlayer => Players.FirstOrDefault(p => p.IsLocalPlayer);
    }

    // Modèle interne pour désérialiser /liveclientdata/gamestats
    internal class GameStats
    {
        [JsonPropertyName("gameTime")]
        public float GameTime { get; set; }
    }

    // Modèle interne pour désérialiser /liveclientdata/activeplayer
    internal class ActivePlayer
    {
        [JsonPropertyName("summonerName")]
        public string SummonerName { get; set; } = "";
    }
}