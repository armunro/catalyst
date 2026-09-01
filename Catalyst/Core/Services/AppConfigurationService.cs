using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Catalyst.Core.Domain.Models;
using Catalyst.Core.Ports.Inbound;
using Catalyst.Core.Ports.Outbound;

namespace Catalyst.Core.Services;

public class AppConfigurationService : IAppConfigurationService
{
    private readonly IConfigRepository _configRepository;
    private readonly ISettingsStorage _settingsStorage;
    private readonly IPathResolver _pathResolver;

    public AppConfigurationService(
        IConfigRepository configRepository,
        ISettingsStorage settingsStorage,
        IPathResolver pathResolver)
    {
        _configRepository = configRepository;
        _settingsStorage = settingsStorage;
        _pathResolver = pathResolver;
    }

    public string ActiveConfigPath => _pathResolver.GetActiveConfigPath();

    public string RootDir => _pathResolver.GetRootDir(ActiveConfigPath);

    public string IconsBaseDir => _pathResolver.GetIconsDir(ActiveConfigPath);

    public string ConfiguredHotkey { get; set; } = "Alt+Space";

    public void SetCustomConfigPath(string? path)
    {
        string? normalizedPath = string.IsNullOrWhiteSpace(path) ? null : Adapters.Persistence.PathResolver.NormalizePath(path);
        _pathResolver.CustomConfigPath = normalizedPath;
        SetConfigPathPreference(normalizedPath);
    }

    public string? GetCustomConfigPath()
    {
        return _pathResolver.CustomConfigPath ?? GetConfigPathPreference();
    }

    public void SetConfigPathPreference(string? path)
    {
        var settings = _settingsStorage.LoadSettings() ?? new UserSettings();
        settings.ConfigPath = string.IsNullOrWhiteSpace(path) ? null : Adapters.Persistence.PathResolver.NormalizePath(path);
        _settingsStorage.SaveSettings(settings);
    }

    public string? GetConfigPathPreference()
    {
        return _settingsStorage.LoadSettings()?.ConfigPath;
    }

    public void SetCustomIconsDir(string? path)
    {
        string? normalized = string.IsNullOrWhiteSpace(path) ? null : Adapters.Persistence.PathResolver.NormalizeDirectoryPath(path);
        _pathResolver.CustomIconsDir = normalized;
        SetIconsDirPreference(normalized);
    }

    public string? GetCustomIconsDir()
    {
        return _pathResolver.CustomIconsDir ?? GetIconsDirPreference();
    }

    public void SetIconsDirPreference(string? path)
    {
        var settings = _settingsStorage.LoadSettings() ?? new UserSettings();
        settings.IconsDir = string.IsNullOrWhiteSpace(path) ? null : Adapters.Persistence.PathResolver.NormalizeDirectoryPath(path);
        _settingsStorage.SaveSettings(settings);
    }

    public string? GetIconsDirPreference()
    {
        return _settingsStorage.LoadSettings()?.IconsDir;
    }

    public List<AppInfo> LoadApps(string? configPath = null)
    {
        string path = configPath ?? ActiveConfigPath;
        string rootDir = _pathResolver.GetRootDir(path);
        string iconsDir = IconsBaseDir;

        var apps = new List<AppInfo>();
        var configFile = _configRepository.LoadConfigFile(path);

        if (configFile != null)
        {
            if (!string.IsNullOrWhiteSpace(configFile.Hotkey))
            {
                ConfiguredHotkey = configFile.Hotkey;
            }

            if (configFile.Apps != null)
            {
                foreach (var entry in configFile.Apps)
                {
                    var appInfo = ConvertToAppInfo(entry, rootDir, iconsDir);
                    apps.Add(appInfo);
                }
            }
        }

        try
        {
            Directory.CreateDirectory(Path.Combine(rootDir, "Logs"));
        }
        catch { }

        return apps;
    }

    public void SaveApps(IEnumerable<AppInfo> apps, string? configPath = null)
    {
        SaveApps(apps, null, configPath);
    }

    public void SaveApps(IEnumerable<AppInfo> apps, string? hotkey = null, string? configPath = null)
    {
        string path = configPath ?? ActiveConfigPath;
        var configFile = CreateConfigFile(apps, hotkey, path);
        _configRepository.SaveConfigFile(configFile, path);
    }

    public AppConfigFile CreateConfigFile(IEnumerable<AppInfo> apps)
    {
        return CreateConfigFile(apps, null, ActiveConfigPath);
    }

    public AppConfigFile CreateConfigFile(IEnumerable<AppInfo> apps, string? hotkey = null)
    {
        return CreateConfigFile(apps, hotkey, ActiveConfigPath);
    }

    public AppConfigFile CreateConfigFile(IEnumerable<AppInfo> apps, string? hotkey, string? configPath)
    {
        string path = configPath ?? ActiveConfigPath;
        string rootDir = _pathResolver.GetRootDir(path);
        var configFile = new AppConfigFile
        {
            Hotkey = hotkey ?? ConfiguredHotkey
        };

        foreach (var app in apps)
        {
            configFile.Apps.Add(ConvertToConfigEntry(app, rootDir));
        }

        return configFile;
    }

    public AppInfo ConvertToAppInfo(AppConfigEntry entry, string rootDir, string iconsDir)
    {
        string projectPath = entry.Launch?.ProjectPath ?? string.Empty;
        if (!string.IsNullOrEmpty(projectPath) && !Path.IsPathRooted(projectPath))
        {
            projectPath = Path.GetFullPath(Path.Combine(rootDir, projectPath));
        }

        string execPath = entry.Launch?.ExecutablePath ?? string.Empty;
        if (!string.IsNullOrEmpty(execPath) && !Path.IsPathRooted(execPath) && !execPath.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && !execPath.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            execPath = Path.GetFullPath(Path.Combine(rootDir, execPath));
        }

        string workingDir = entry.Launch?.WorkingDirectory ?? string.Empty;
        if (!string.IsNullOrEmpty(workingDir) && !Path.IsPathRooted(workingDir))
        {
            workingDir = Path.GetFullPath(Path.Combine(rootDir, workingDir));
        }

        string iconPath = entry.Icon?.IconPath ?? string.Empty;
        if (!string.IsNullOrEmpty(iconPath) && !Path.IsPathRooted(iconPath))
        {
            iconPath = Path.GetFullPath(Path.Combine(rootDir, iconPath));
        }

        string faviconPath = entry.Icon?.FaviconPath ?? string.Empty;
        if (!string.IsNullOrEmpty(faviconPath) && !Path.IsPathRooted(faviconPath))
        {
            faviconPath = Path.GetFullPath(Path.Combine(rootDir, faviconPath));
        }

        string customGlyphSvg = entry.Icon?.CustomGlyphSvg ?? string.Empty;
        if (!string.IsNullOrEmpty(customGlyphSvg) && !customGlyphSvg.StartsWith("<") && !Path.IsPathRooted(customGlyphSvg))
        {
            string candidate = Path.GetFullPath(Path.Combine(rootDir, customGlyphSvg));
            if (File.Exists(candidate)) customGlyphSvg = candidate;
        }

        string svgOverride = entry.Icon?.SvgOverride ?? string.Empty;
        if (!string.IsNullOrEmpty(svgOverride) && !svgOverride.StartsWith("<") && !Path.IsPathRooted(svgOverride))
        {
            string candidate = Path.GetFullPath(Path.Combine(rootDir, svgOverride));
            if (File.Exists(candidate)) svgOverride = candidate;
        }

        var appInfo = new AppInfo
        {
            Name = entry.Name ?? string.Empty,
            ProjectPath = projectPath,
            ExecutablePath = execPath,
            Arguments = entry.Launch?.Arguments ?? string.Empty,
            WorkingDirectory = workingDir,
            RunAsAdmin = entry.Launch?.RunAsAdmin ?? false,
            Color = !string.IsNullOrWhiteSpace(entry.Icon?.Color) ? entry.Icon.Color : "#1E88E4",
            SecondaryColor = entry.Icon?.SecondaryColor ?? string.Empty,
            BackgroundType = !string.IsNullOrWhiteSpace(entry.Icon?.BackgroundType) ? entry.Icon.BackgroundType : (!string.IsNullOrWhiteSpace(entry.Icon?.SecondaryColor) ? "Gradient" : "Solid"),
            GradientDirection = !string.IsNullOrWhiteSpace(entry.Icon?.GradientDirection) ? entry.Icon.GradientDirection : "Diagonal",
            Label = entry.Icon?.Label ?? string.Empty,
            IconPath = iconPath,
            FaviconPath = faviconPath,
            BootstrapIcon = entry.Icon?.BootstrapIcon ?? string.Empty,
            CustomGlyphSvg = customGlyphSvg,
            CustomGlyphColor = entry.Icon?.CustomGlyphColor ?? string.Empty,
            SvgOverride = svgOverride,
            IsHidden = entry.Hidden
        };

        if (string.IsNullOrEmpty(appInfo.IconPath))
        {
            string defaultIconPath = Path.Combine(iconsDir, appInfo.Name, $"{appInfo.Name}.png");
            if (File.Exists(defaultIconPath))
            {
                appInfo.IconPath = defaultIconPath;
            }
        }

        if (string.IsNullOrEmpty(appInfo.FaviconPath))
        {
            string appIconDir = Path.Combine(iconsDir, appInfo.Name);
            string icoPath = Path.Combine(appIconDir, "favicon.ico");
            string png32Path = Path.Combine(appIconDir, "favicon-32x32.png");
            string pngPath = Path.Combine(appIconDir, $"{appInfo.Name}.png");
            string svgPath = Path.Combine(appIconDir, $"{appInfo.Name}.svg");

            if (File.Exists(icoPath))
            {
                appInfo.FaviconPath = icoPath;
            }
            else if (File.Exists(png32Path))
            {
                appInfo.FaviconPath = png32Path;
            }
            else if (File.Exists(pngPath))
            {
                appInfo.FaviconPath = pngPath;
            }
            else if (File.Exists(svgPath))
            {
                appInfo.FaviconPath = svgPath;
            }
        }

        return appInfo;
    }

    public AppConfigEntry ConvertToConfigEntry(AppInfo app, string rootDir)
    {
        string projectPath = app.ProjectPath;
        if (!string.IsNullOrEmpty(projectPath) && !string.IsNullOrEmpty(rootDir) && projectPath.StartsWith(rootDir, StringComparison.OrdinalIgnoreCase))
        {
            projectPath = Path.GetRelativePath(rootDir, projectPath);
        }

        string execPath = app.ExecutablePath;
        if (!string.IsNullOrEmpty(execPath) && !string.IsNullOrEmpty(rootDir) && execPath.StartsWith(rootDir, StringComparison.OrdinalIgnoreCase))
        {
            execPath = Path.GetRelativePath(rootDir, execPath);
        }

        string iconPath = app.IconPath;
        if (!string.IsNullOrEmpty(iconPath) && !string.IsNullOrEmpty(rootDir) && iconPath.StartsWith(rootDir, StringComparison.OrdinalIgnoreCase))
        {
            iconPath = Path.GetRelativePath(rootDir, iconPath);
        }

        string faviconPath = app.FaviconPath;
        if (!string.IsNullOrEmpty(faviconPath) && !string.IsNullOrEmpty(rootDir) && faviconPath.StartsWith(rootDir, StringComparison.OrdinalIgnoreCase))
        {
            faviconPath = Path.GetRelativePath(rootDir, faviconPath);
        }

        string customGlyphSvg = app.CustomGlyphSvg;
        if (!string.IsNullOrEmpty(customGlyphSvg) && !string.IsNullOrEmpty(rootDir) && customGlyphSvg.StartsWith(rootDir, StringComparison.OrdinalIgnoreCase))
        {
            customGlyphSvg = Path.GetRelativePath(rootDir, customGlyphSvg);
        }

        string svgOverride = app.SvgOverride;
        if (!string.IsNullOrEmpty(svgOverride) && !string.IsNullOrEmpty(rootDir) && svgOverride.StartsWith(rootDir, StringComparison.OrdinalIgnoreCase))
        {
            svgOverride = Path.GetRelativePath(rootDir, svgOverride);
        }

        return new AppConfigEntry
        {
            Name = app.Name,
            Hidden = app.IsHidden,
            Launch = new LaunchConfig
            {
                ProjectPath = projectPath,
                ExecutablePath = execPath,
                Arguments = app.Arguments,
                WorkingDirectory = app.WorkingDirectory,
                RunAsAdmin = app.RunAsAdmin
            },
            Icon = new IconConfig
            {
                Color = app.Color,
                SecondaryColor = app.SecondaryColor,
                BackgroundType = app.BackgroundType,
                GradientDirection = app.GradientDirection,
                Label = app.Label,
                FaviconPath = faviconPath,
                BootstrapIcon = app.BootstrapIcon,
                CustomGlyphSvg = customGlyphSvg,
                CustomGlyphColor = app.CustomGlyphColor,
                SvgOverride = svgOverride,
                IconPath = iconPath
            }
        };
    }
}
