using LoLProximityChat.Core.Core;
using LoLProximityChat.Core.Models;

namespace LoLProximityChat.WPF.ViewModels
{
    public class SettingsViewModel
    {
        private readonly ConfigManager _configManager;
        public AppConfig Config { get; private set; }

        public SettingsViewModel(ConfigManager configManager)
        {
            _configManager = configManager;
            Config         = configManager.Default();
        }

        public bool Save(string discordUsername)
        {
            if (string.IsNullOrWhiteSpace(discordUsername))
                return false;

            Config.DiscordUsername = discordUsername.Trim();
            _configManager.Save(Config);
            return true;
        }
    }
}