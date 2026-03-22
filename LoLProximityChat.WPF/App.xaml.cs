using System.Configuration;
using System.Data;
using System.Windows;

namespace LoLProximityChat.WPF;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : System.Windows.Application
{
    protected override void OnExit(ExitEventArgs e)
    {
        base.OnExit(e);
        Environment.Exit(0); // Force kill de tous les threads
    }
}