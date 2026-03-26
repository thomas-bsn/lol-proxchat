using System;
using System.Windows;
using LoLProximityChat.Core.Core;
using LoLProximityChat.WPF.Views;

namespace LoLProximityChat.WPF;

public partial class App : System.Windows.Application
{
    public App()
    {
        this.DispatcherUnhandledException += (s, e) =>
        {
            System.Windows.MessageBox.Show(e.Exception.ToString(), "UI Thread Exception");
            e.Handled = true;
        };

        AppDomain.CurrentDomain.UnhandledException += (s, e) =>
        {
            System.Windows.MessageBox.Show(e.ExceptionObject.ToString(), "Non-UI Exception");
        };
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var configManager = new ConfigManager();

        if (!configManager.Exists())
        {
            var settings = new SettingsWindow(configManager);
            var result = settings.ShowDialog();

            if (result != true)
            {
                Shutdown();
                return;
            }
        }

        var config = configManager.Load();

        var main = new MainWindow();
        main.Initialize(config);
        main.Show();
    }
}