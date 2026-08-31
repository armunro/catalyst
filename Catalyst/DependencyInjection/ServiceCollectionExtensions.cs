using System;
using Catalyst.Adapters.Icons;
using Catalyst.Adapters.Persistence;
using Catalyst.Adapters.Platform;
using Catalyst.Adapters.Processes;
using Catalyst.Adapters.UI.Navigation;
using Catalyst.Adapters.UI.ViewModels;
using Catalyst.Core.Ports.Inbound;
using Catalyst.Core.Ports.Outbound;
using Catalyst.Core.Services;
using Catalyst.Windows;
using Microsoft.Extensions.DependencyInjection;

namespace Catalyst.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddCatalystServices(this IServiceCollection services)
    {
        // Driven Outbound Adapters
        services.AddSingleton<IConfigRepository, YamlConfigRepository>();
        services.AddSingleton<ISettingsStorage, JsonSettingsStorage>();
        services.AddSingleton<IPathResolver, PathResolver>();
        services.AddSingleton<IProcessExecutor, WindowsProcessExecutor>();
        services.AddSingleton<IIconRenderer, SkiaIconRenderer>();
        services.AddSingleton<IGlobalHotkeyHook, WindowsHotkeyHook>();
        services.AddSingleton<IWindowPlacementService, WindowPlacementService>();

        // Core Application Services (Driving / Inbound Ports)
        services.AddSingleton<IAppConfigurationService, AppConfigurationService>();
        services.AddSingleton<IAppLauncherService, AppLauncherService>();
        services.AddSingleton<IIconManagementService, IconManagementService>();
        services.AddSingleton<IHotkeyService, HotkeyService>();

        // UI & Navigation
        services.AddSingleton<IWindowService, WindowService>();
        services.AddTransient<MainWindowViewModel>();
        services.AddTransient<AppManagementViewModel>();

        // Views
        services.AddTransient<MainWindow>();
        services.AddTransient<AppManagementWindow>();

        return services;
    }
}
