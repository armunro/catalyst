using System;
using System.Collections.Generic;
using Catalyst.Adapters.Persistence;
using Catalyst.Core.Domain.Models;
using Catalyst.Core.Ports.Inbound;
using Catalyst.Core.Ports.Outbound;
using Catalyst.Core.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Catalyst.Services;

/// <summary>
/// Backward-compatible facade delegating to hexagonal ports and adapters.
/// </summary>
public static class ConfigService
{
    private static readonly ISettingsStorage DefaultSettingsStorage = new JsonSettingsStorage();
    private static readonly IPathResolver DefaultPathResolver = new PathResolver(DefaultSettingsStorage);
    private static readonly IConfigRepository DefaultConfigRepository = new YamlConfigRepository();
    private static readonly IAppConfigurationService DefaultAppConfigService = new AppConfigurationService(DefaultConfigRepository, DefaultSettingsStorage, DefaultPathResolver);

    private static IPathResolver GetPathResolver() =>
        App.Services?.GetService<IPathResolver>() ?? DefaultPathResolver;

    private static ISettingsStorage GetSettingsStorage() =>
        App.Services?.GetService<ISettingsStorage>() ?? DefaultSettingsStorage;

    private static IConfigRepository GetConfigRepository() =>
        App.Services?.GetService<IConfigRepository>() ?? DefaultConfigRepository;

    private static IAppConfigurationService GetAppConfigService() =>
        App.Services?.GetService<IAppConfigurationService>() ?? DefaultAppConfigService;

    public static string SettingsFilePath
    {
        get => GetSettingsStorage().SettingsFilePath;
        set => GetSettingsStorage().SettingsFilePath = value;
    }

    public static string? CustomConfigPath
    {
        get => GetPathResolver().CustomConfigPath;
        set => GetPathResolver().CustomConfigPath = value;
    }

    public static string? ParseCommandLineConfigPath(string[]? args = null) =>
        GetPathResolver().ParseCommandLineConfigPath(args);

    public static string? GetEnvironmentConfigPath() =>
        GetPathResolver().GetEnvironmentConfigPath();

    public static string? GetUserConfigPath() =>
        GetSettingsStorage().LoadSettings()?.ConfigPath;

    public static void SetUserConfigPath(string? path)
    {
        var storage = GetSettingsStorage();
        var settings = storage.LoadSettings() ?? new UserSettings();
        settings.ConfigPath = path;
        storage.SaveSettings(settings);
    }

    public static string GetDefaultConfigPath() =>
        GetPathResolver().GetDefaultConfigPath();

    public static string GetActiveConfigPath(string[]? args = null) =>
        GetPathResolver().GetActiveConfigPath(args);

    public static string GetRootDir(string? configPath = null) =>
        GetPathResolver().GetRootDir(configPath);

    public static AppConfigFile? LoadConfigFile(string configPath) =>
        GetConfigRepository().LoadConfigFile(configPath);

    public static void SaveConfigFile(AppConfigFile config, string configPath) =>
        GetConfigRepository().SaveConfigFile(config, configPath);

    public static AppConfigFile CreateConfigFile(IEnumerable<AppInfo> apps, string hotkey, string rootDir) =>
        GetAppConfigService().CreateConfigFile(apps, hotkey);

    public static void SaveConfigFile(IEnumerable<AppInfo> apps, string hotkey, string configPath) =>
        GetAppConfigService().SaveApps(apps, hotkey, configPath);
}
