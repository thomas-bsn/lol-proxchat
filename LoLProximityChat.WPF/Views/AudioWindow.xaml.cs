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
        {
            var vm = DataContext as AudioViewModel;
            if (vm is null) return;

            var changed = vm.SaveServerUrl();

            if (changed)
                MessageBox.Show(
                    "URL sauvegardée.\nRedémarre l'application pour te connecter au nouveau serveur.",
                    "Redémarrage requis",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            else
                MessageBox.Show(
                    "URL sauvegardée.",
                    "Sauvegardé",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
        }
    }
}