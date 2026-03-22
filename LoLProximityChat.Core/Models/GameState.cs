using System.Text.Json.Serialization;

namespace LoLProximityChat.Core.Models
{
    public class ActivePlayer
    {
        [JsonPropertyName("summonerName")]
        public string SummonerName { get; set; } = "";
    }

    // NOUVEAU — mappe /liveclientdata/gamestats
    public class GameStats
    {
        [JsonPropertyName("gameTime")]
        public float GameTime { get; set; }
    }

    public class GameState
    {
        public bool   IsInGame         { get; set; }
        public string LocalPlayerName  { get; set; } = "";
        public List<PlayerInfo> Players { get; set; } = [];
        public float  GameTime         { get; set; } // NOUVEAU

        public List<PlayerInfo> OrderTeam => Players.Where(p => p.Team == "ORDER").ToList();
        public List<PlayerInfo> ChaosTeam => Players.Where(p => p.Team == "CHAOS").ToList();
        public PlayerInfo? LocalPlayer   => Players.FirstOrDefault(p => p.IsLocalPlayer);
    }
}