namespace LoLProximityChat.Core.Models
{
    public class PlayerMapPosition
    {
        public string SummonerName { get; set; } = "";
        public string ChampionName { get; set; } = "";
        public string Team         { get; set; } = "";
        public float  X            { get; set; }
        public float  Y            { get; set; }
        public bool   IsVisible    { get; set; }
    }
}