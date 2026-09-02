using System;
using System.Windows;
using Catalyst.Adapters.Platform;
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
    private static SingleInstanceManager? _singleInstanceManager;

    protected override void OnStartup(StartupEventArgs e)
    {
        _singleInstanceManager = new SingleInstanceManager();
        if (!_singleInstanceManager.TryAcquireSingleInstance(() =>
            {
                Dispatcher.BeginInvoke(() =>
                {
                    if (MainWindow is MainWindow win)
                    {
                        win.RestoreWindow();
                    }
                });
            }))
        {
            _singleInstanceManager.NotifyExistingInstance();
            _singleInstanceManager.Dispose();
            _singleInstanceManager = null;
            Shutdown();
            return;
        }

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

    protected override void OnExit(ExitEventArgs e)
    {
        _singleInstanceManager?.Dispose();
        _singleInstanceManager = null;
        base.OnExit(e);
    }
}
