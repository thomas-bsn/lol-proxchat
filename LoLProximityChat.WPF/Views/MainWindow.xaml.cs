using System.Windows;
using LoLProximityChat.WPF.ViewModels;

namespace LoLProximityChat.WPF.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow() => InitializeComponent();

        private void OnOpenCalibration(object sender, RoutedEventArgs e)
            => new CalibrationOverlay().Show();

        private void OnOpenAudio(object sender, RoutedEventArgs e)
            => new AudioWindow((DataContext as MainViewModel)!.Audio).Show();

        private void OnToggleMute(object sender, RoutedEventArgs e)
            => (DataContext as MainViewModel)?.Audio.ToggleMicMute();

        protected override void OnClosed(EventArgs e)
        {
            if (DataContext is MainViewModel vm)
                vm.DisposeAsync().AsTask().Wait();
            base.OnClosed(e);
        }
    }
}