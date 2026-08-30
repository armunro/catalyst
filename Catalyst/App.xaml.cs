using System.Windows;
using Catalyst.Services;
using Catalyst.Windows;

namespace Catalyst;

public partial class App : System.Windows.Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        string? cliConfig = ConfigService.ParseCommandLineConfigPath(e.Args);
        if (!string.IsNullOrWhiteSpace(cliConfig))
        {
            ConfigService.CustomConfigPath = cliConfig;
        }
    }
}