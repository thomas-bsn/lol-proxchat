using System.Windows;
using LoLProximityChat.WPF.ViewModels;

namespace LoLProximityChat.WPF.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow() => InitializeComponent();

        protected override void OnClosed(EventArgs e)
        {
            if (DataContext is MainViewModel vm)
                vm.DisposeAsync().AsTask().Wait();
            base.OnClosed(e);
        }
        private void OnOpenCalibration(object sender, RoutedEventArgs e)
            => new CalibrationOverlay().Show();
    }
}