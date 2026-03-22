using System.Windows;
using LoLProximityChat.WPF.ViewModels;

namespace LoLProximityChat.WPF.Views
{
    public partial class CalibrationOverlay : Window
    {
        public CalibrationOverlay() => InitializeComponent();

        private void OnSave(object sender, RoutedEventArgs e)
            => (DataContext as CalibrationViewModel)?.Save();

        private void OnReset(object sender, RoutedEventArgs e)
            => (DataContext as CalibrationViewModel)?.Reset();

        private void OnClose(object sender, RoutedEventArgs e)
            => Close();

        protected override void OnClosed(EventArgs e)
        {
            (DataContext as CalibrationViewModel)?.Dispose();
            base.OnClosed(e);
        }
    }
}