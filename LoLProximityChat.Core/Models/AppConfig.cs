using System.Text.Json;

namespace LoLProximityChat.Core.Models
{
    public class AppConfig
    {
        public int MinimapX      { get; set; }
        public int MinimapY      { get; set; }
        public int MinimapSize   { get; set; }
        public string ServerUrl { get; set; } = "http://localhost:5128";

        private static readonly string ConfigPath =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "LoLProximityChat", "config.json");

        public static AppConfig Load()
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

        public void Save()
        {
            var dir = Path.GetDirectoryName(ConfigPath)!;
            Directory.CreateDirectory(dir);
            File.WriteAllText(ConfigPath, JsonSerializer.Serialize(this));
        }

        public static AppConfig Default()
        {
            var screenW = (int)System.Windows.Forms.Screen.PrimaryScreen!.Bounds.Width;
            var screenH = (int)System.Windows.Forms.Screen.PrimaryScreen!.Bounds.Height;
            var size = (int)(screenH * 0.1525);
            return new AppConfig
            {
                MinimapX    = screenW - size - 4,
                MinimapY    = screenH - size - 4,
                MinimapSize = size
            };
        }
    }
}