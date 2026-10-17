using System.Configuration;
using System.Data;
using System.Windows;
using Atlas.Tester.Engine;

namespace Atlas.Tester.Wpf;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        ClientDataPaths.EnsureDirectories();
        base.OnStartup(e);
    }
}

