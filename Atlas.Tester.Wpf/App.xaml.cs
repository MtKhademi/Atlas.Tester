using System.Configuration;
using System.Data;
using System.Windows;
using System.IO;
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
        ClientDataPaths.MigrateLegacyApplicationData();
        ClientDataPaths.MigrateDirectory(Path.Combine(AppContext.BaseDirectory, "scenarios"), ClientDataPaths.ScenariosDirectory);
        ClientDataPaths.MigrateDirectory(Path.Combine(AppContext.BaseDirectory, "reports"), ClientDataPaths.ReportsDirectory);
        base.OnStartup(e);
    }
}

