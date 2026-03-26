using System.Text.Json;
using LoLProximityChat.Core.Models;

namespace LoLProximityChat.Core.Core
{
    public class ConfigManager
    {
        private static readonly string ConfigPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "LoLProximityChat", "config.json");

        public bool Exists() => File.Exists(ConfigPath);

        public AppConfig Load()
        {
            try
            {
                if (File.Exists(ConfigPath))
                {
                    var json = File.ReadAllText(ConfigPath);
                    return JsonSerializer.Deserialize<AppConfig>(json) ?? Default();
                }
            }
            catch { }
            return Default();
        }

        public void Save(AppConfig config)
        {
            var dir = Path.GetDirectoryName(ConfigPath)!;
            Directory.CreateDirectory(dir);
            File.WriteAllText(ConfigPath, JsonSerializer.Serialize(config));
        }

        public AppConfig Default()
        {
            var screenW = (int)System.Windows.Forms.Screen.PrimaryScreen!.Bounds.Width;
            var screenH = (int)System.Windows.Forms.Screen.PrimaryScreen!.Bounds.Height;
            var size    = (int)(screenH * 0.1525);

            #if DEBUG
                        var serverUrl          = "http://localhost:5128";
                        var discordRedirectUri = "http://localhost:5128/auth/discord/callback";
            #else
                var serverUrl          = "https://lol-proxchat-production.up.railway.app";
                var discordRedirectUri = "https://lol-proxchat-production.up.railway.app/auth/discord/callback";
            #endif

            return new AppConfig
            {
                MinimapX           = screenW - size - 4,
                MinimapY           = screenH - size - 4,
                MinimapSize        = size,
                ServerUrl          = serverUrl,
                DiscordRedirectUri = discordRedirectUri,
                DiscordClientId    = "989513523359006800"
            };
        }
    }
}