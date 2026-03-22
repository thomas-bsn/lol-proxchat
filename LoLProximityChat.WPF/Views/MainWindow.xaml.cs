using System.Windows;
using LoLProximityChat.WPF.ViewModels;

namespace LoLProximityChat.WPF.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow() => InitializeComponent();

        protected override void OnClosed(EventArgs e)
        {
            (DataContext as MainViewModel)?.Dispose();
            base.OnClosed(e);
        }
        private void OnOpenCalibration(object sender, RoutedEventArgs e)
            => new CalibrationOverlay().Show();
    }
}