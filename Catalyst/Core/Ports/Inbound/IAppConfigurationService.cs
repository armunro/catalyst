using System.Collections.Generic;

namespace Catalyst.Core.Ports.Inbound;

public interface IAppConfigurationService
{
    string ActiveConfigPath { get; }
    string RootDir { get; }
    string IconsBaseDir { get; }
    string ConfiguredHotkey { get; set; }

    List<AppInfo> LoadApps(string? configPath = null);
    void SaveApps(IEnumerable<AppInfo> apps, string? hotkey = null, string? configPath = null);
    AppConfigFile CreateConfigFile(IEnumerable<AppInfo> apps, string? hotkey = null);
    AppInfo ConvertToAppInfo(AppConfigEntry entry, string rootDir, string iconsDir);
    AppConfigEntry ConvertToConfigEntry(AppInfo app, string rootDir);
    void SetCustomConfigPath(string? path);
    string? GetCustomConfigPath();
    void SetConfigPathPreference(string? path);
    string? GetConfigPathPreference();
}
