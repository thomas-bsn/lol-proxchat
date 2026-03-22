using System.Windows;
using LoLProximityChat.WPF.ViewModels;

namespace LoLProximityChat.WPF.Views
{
    public partial class AudioWindow : Window
    {
        public AudioWindow(AudioViewModel vm)
        {
            InitializeComponent();
            DataContext = vm;
        }

        private void OnMutePlayer(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement { DataContext: PlayerAudioEntry entry })
                entry.IsMuted = !entry.IsMuted;
        }

        private void OnSaveServerUrl(object sender, RoutedEventArgs e)
            => (DataContext as AudioViewModel)?.SaveServerUrl();
    }
}