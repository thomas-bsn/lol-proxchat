namespace LoLProximityChat.WPF.ViewModels
{
    public class PlayerViewModel
    {
        public string Name        { get; set; } = "";
        public char   Initial     { get; set; }
        public string Role        { get; set; } = "";
        public bool   IsLocal     { get; set; }
        public bool   IsSpeaking  { get; set; }
        public bool   IsOutOfRange { get; set; }
    }
}