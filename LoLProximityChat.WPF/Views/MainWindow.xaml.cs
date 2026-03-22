using System.Windows;
using LoLProximityChat.WPF.ViewModels;

namespace LoLProximityChat.WPF.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow() => InitializeComponent();

        private void OnOpenCalibration(object sender, RoutedEventArgs e)
        {
            var overlay = new CalibrationOverlay();
            var lolProcess = System.Diagnostics.Process
                .GetProcessesByName("League of Legends")
                .FirstOrDefault();
            if (lolProcess != null)
            {
                var screen = System.Windows.Forms.Screen.FromHandle(lolProcess.MainWindowHandle);
                overlay.Left = screen.Bounds.Left;
                overlay.Top  = screen.Bounds.Top;
            }
            overlay.Show();
        }

        private void OnOpenAudio(object sender, RoutedEventArgs e)
            => new AudioWindow((DataContext as MainViewModel)!.Audio).Show();

        private void OnToggleMute(object sender, RoutedEventArgs e)
            => (DataContext as MainViewModel)?.Audio.ToggleMicMute();

        private void OnReconnect(object sender, RoutedEventArgs e)
            => (DataContext as MainViewModel)?.Reconnect();

        protected override void OnClosed(EventArgs e)
        {
            if (DataContext is MainViewModel vm)
                vm.DisposeAsync().AsTask().Wait();
            base.OnClosed(e);
        }
    }
}