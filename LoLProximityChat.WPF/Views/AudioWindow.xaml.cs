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
            System.Windows.MessageBox.Show(
                changed
                    ? "URL sauvegardée.\nRedémarre l'application pour te connecter au nouveau serveur."
                    : "URL sauvegardée.",
                changed ? "Redémarrage requis" : "Sauvegardé",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private void OnSaveMyDiscord(object sender, RoutedEventArgs e)
        {
            var vm = DataContext as AudioViewModel;
            if (vm is null) return;
            vm.SaveMyDiscord();
            System.Windows.MessageBox.Show(
                "Pseudo Discord sauvegardé.",
                "Sauvegardé",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
    }
}