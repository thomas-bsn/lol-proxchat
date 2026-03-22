using System.Windows;
using LoLProximityChat.WPF.ViewModels;

namespace LoLProximityChat.WPF.Views
{
    public partial class CalibrationWindow : Window
    {
        public CalibrationWindow() => InitializeComponent();

        private void OnSave(object sender, RoutedEventArgs e)
            => (DataContext as CalibrationViewModel)?.Save();

        private void OnReset(object sender, RoutedEventArgs e)
            => (DataContext as CalibrationViewModel)?.Reset();
        
    }
}