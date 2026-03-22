using System.ComponentModel;
using System.Runtime.CompilerServices;
using LoLProximityChat.Core.Models;

namespace LoLProximityChat.WPF.ViewModels
{
    public class CalibrationViewModel : INotifyPropertyChanged
    {
        private AppConfig _config;

        public CalibrationViewModel()
        {
            _config     = AppConfig.Load();
            MinimapX    = _config.MinimapX;
            MinimapY    = _config.MinimapY;
            MinimapSize = _config.MinimapSize;
        }

        private int _minimapX;
        public int MinimapX
        {
            get => _minimapX;
            set { _minimapX = value; OnPropertyChanged(); OnPropertyChanged(nameof(PositionLabel)); }
        }

        private int _minimapY;
        public int MinimapY
        {
            get => _minimapY;
            set { _minimapY = value; OnPropertyChanged(); OnPropertyChanged(nameof(PositionLabel)); }
        }

        private int _minimapSize;
        public int MinimapSize
        {
            get => _minimapSize;
            set { _minimapSize = value; OnPropertyChanged(); OnPropertyChanged(nameof(PositionLabel)); }
        }

        public string PositionLabel => $"X: {MinimapX}  Y: {MinimapY}  Taille: {MinimapSize}px";

        private string _statusText = "";
        public string StatusText
        {
            get => _statusText;
            set { _statusText = value; OnPropertyChanged(); }
        }

        public void Save()
        {
            _config.MinimapX    = MinimapX;
            _config.MinimapY    = MinimapY;
            _config.MinimapSize = MinimapSize;
            _config.Save();
            StatusText = "✓ Sauvegardé";
        }

        public void Reset()
        {
            var def     = AppConfig.Default();
            MinimapX    = def.MinimapX;
            MinimapY    = def.MinimapY;
            MinimapSize = def.MinimapSize;
            StatusText  = "Réinitialisé";
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
    }
}