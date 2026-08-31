using System;
using System.Windows;
using Catalyst.Core.Ports.Inbound;
using Catalyst.Core.Ports.Outbound;
using Catalyst.DependencyInjection;
using Catalyst.Services;
using Catalyst.Windows;
using Microsoft.Extensions.DependencyInjection;

namespace Catalyst;

public partial class App : System.Windows.Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var serviceCollection = new ServiceCollection();
        serviceCollection.AddCatalystServices();

        Services = serviceCollection.BuildServiceProvider();

        var pathResolver = Services.GetRequiredService<IPathResolver>();
        string? cliConfig = pathResolver.ParseCommandLineConfigPath(e.Args);
        if (!string.IsNullOrWhiteSpace(cliConfig))
        {
            pathResolver.CustomConfigPath = cliConfig;
            ConfigService.CustomConfigPath = cliConfig;
        }

        var mainWindow = Services.GetRequiredService<MainWindow>();
        mainWindow.Show();
    }
}
