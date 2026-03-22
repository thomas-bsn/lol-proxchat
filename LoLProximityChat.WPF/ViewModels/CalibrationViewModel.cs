using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media.Imaging;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Threading;
using LoLProximityChat.Core.Models;

namespace LoLProximityChat.WPF.ViewModels
{
    public class CalibrationViewModel : INotifyPropertyChanged, IDisposable
    {
        private readonly DispatcherTimer _timer = new();
        private AppConfig _config;

        public CalibrationViewModel()
        {
            _config = AppConfig.Load();
            MinimapX    = _config.MinimapX;
            MinimapY    = _config.MinimapY;
            MinimapSize = _config.MinimapSize;
        }

        // ── Bindable ──────────────────────────────────────────────────────────
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

        private BitmapSource? _preview;
        public BitmapSource? Preview
        {
            get => _preview;
            set { _preview = value; OnPropertyChanged(); }
        }

        private string _statusText = "";
        public string StatusText
        {
            get => _statusText;
            set { _statusText = value; OnPropertyChanged(); }
        }

        // ── Commands ──────────────────────────────────────────────────────────
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
            var def = AppConfig.Default();
            MinimapX    = def.MinimapX;
            MinimapY    = def.MinimapY;
            MinimapSize = def.MinimapSize;
            StatusText  = "Réinitialisé";
        }

        // ── Preview ───────────────────────────────────────────────────────────
        private void RefreshPreview()
        {
            try
            {
                using var bmp = new Bitmap(MinimapSize, MinimapSize);
                using var g   = Graphics.FromImage(bmp);
                g.CopyFromScreen(MinimapX, MinimapY, 0, 0,
                    new System.Drawing.Size(MinimapSize, MinimapSize));
                Preview = ToBitmapSource(bmp);
                StatusText = "";
            }
            catch
            {
                StatusText = "Impossible de capturer l'écran";
            }
        }

        private static BitmapSource ToBitmapSource(Bitmap bmp)
        {
            using var ms = new MemoryStream();
            bmp.Save(ms, ImageFormat.Png);
            ms.Position = 0;
            var bi = new BitmapImage();
            bi.BeginInit();
            bi.CacheOption  = BitmapCacheOption.OnLoad;
            bi.StreamSource = ms;
            bi.EndInit();
            bi.Freeze();
            return bi;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

        public void Dispose() => _timer.Stop();
    }
}