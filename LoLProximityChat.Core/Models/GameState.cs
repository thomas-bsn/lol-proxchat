using System.Text.Json.Serialization;

namespace LoLProximityChat.Core.Models
{
    // Réponse de /liveclientdata/activeplayer
    public class ActivePlayer
    {
        [JsonPropertyName("summonerName")]
        public string SummonerName { get; set; } = "";
    }

    // Ce qu'on expose aux couches supérieures après chaque poll
    public class GameState
    {
        public bool IsInGame { get; set; }
        public string LocalPlayerName { get; set; } = "";
        public List<PlayerInfo> Players { get; set; } = [];

        public List<PlayerInfo> OrderTeam => Players.Where(p => p.Team == "ORDER").ToList();
        public List<PlayerInfo> ChaosTeam => Players.Where(p => p.Team == "CHAOS").ToList();
        public PlayerInfo? LocalPlayer => Players.FirstOrDefault(p => p.IsLocalPlayer);
    }
}