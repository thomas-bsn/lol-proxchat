using System.Windows;
using LoLProximityChat.Core.Core;
using LoLProximityChat.WPF.ViewModels;

namespace LoLProximityChat.WPF.Views
{
    public partial class SettingsWindow : Window
    {
        private readonly SettingsViewModel _vm;

        public SettingsWindow(ConfigManager configManager)
        {
            InitializeComponent();
            _vm = new SettingsViewModel(configManager);
        }

        private void OnSaveClick(object sender, RoutedEventArgs e)
        {
            var saved = _vm.Save(DiscordUsernameBox.Text);
            if (!saved)
            {
                ErrorText.Text       = "Le pseudo Discord ne peut pas être vide.";
                ErrorText.Visibility = Visibility.Visible;
                return;
            }

            DialogResult = true;
            Close();
        }
    }
}