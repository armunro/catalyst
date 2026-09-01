using System;
using System.IO;
using Catalyst.Core.Ports.Outbound;

namespace Catalyst.Adapters.Persistence;

public class PathResolver : IPathResolver
{
    private const string DefaultFileName = "apps.yaml";
    private const string DefaultIconsDirName = "icons";
    private readonly ISettingsStorage _settingsStorage;
    private string? _customConfigPath;
    private string? _customIconsDir;

    public PathResolver(ISettingsStorage settingsStorage)
    {
        _settingsStorage = settingsStorage;
    }

    public string? CustomConfigPath
    {
        get => _customConfigPath;
        set => _customConfigPath = string.IsNullOrWhiteSpace(value) ? null : NormalizePath(value);
    }

    public string? CustomIconsDir
    {
        get => _customIconsDir;
        set => _customIconsDir = string.IsNullOrWhiteSpace(value) ? null : NormalizeDirectoryPath(value);
    }

    public static string NormalizeDirectoryPath(string rawPath)
    {
        if (string.IsNullOrWhiteSpace(rawPath)) return string.Empty;

        string path = rawPath.Trim().Trim('\"', '\'').Trim();
        if (string.IsNullOrWhiteSpace(path)) return string.Empty;

        try
        {
            path = Environment.ExpandEnvironmentVariables(path);

            if (path.StartsWith("~/", StringComparison.Ordinal) || path.StartsWith("~\\", StringComparison.Ordinal))
            {
                string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                path = Path.Combine(home, path[2..]);
            }
            else if (path == "~")
            {
                path = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            }

            return Path.GetFullPath(path);
        }
        catch
        {
            return path;
        }
    }

    public static string NormalizePath(string rawPath)
    {
        if (string.IsNullOrWhiteSpace(rawPath)) return string.Empty;

        string path = rawPath.Trim().Trim('\"', '\'').Trim();
        if (string.IsNullOrWhiteSpace(path)) return string.Empty;

        try
        {
            path = Environment.ExpandEnvironmentVariables(path);

            if (path.StartsWith("~/", StringComparison.Ordinal) || path.StartsWith("~\\", StringComparison.Ordinal))
            {
                string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                path = Path.Combine(home, path[2..]);
            }
            else if (path == "~")
            {
                path = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            }

            if (Directory.Exists(path) || path.EndsWith(Path.DirectorySeparatorChar) || path.EndsWith(Path.AltDirectorySeparatorChar))
            {
                path = Path.Combine(path, DefaultFileName);
            }

            return Path.GetFullPath(path);
        }
        catch
        {
            return path;
        }
    }

    public string? ParseCommandLineConfigPath(string[]? args = null)
    {
        args ??= Environment.GetCommandLineArgs();
        if (args == null || args.Length == 0) return null;

        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i].Trim().Trim('\"', '\'').Trim();

            // Match flags: --config, -c, /config, /c
            if (arg.Equals("--config", StringComparison.OrdinalIgnoreCase) ||
                arg.Equals("-c", StringComparison.OrdinalIgnoreCase) ||
                arg.Equals("/config", StringComparison.OrdinalIgnoreCase) ||
                arg.Equals("/c", StringComparison.OrdinalIgnoreCase))
            {
                if (i + 1 < args.Length && !string.IsNullOrWhiteSpace(args[i + 1]))
                {
                    return NormalizePath(args[i + 1]);
                }
            }

            // Match prefixes: --config=, --config:, -c=, -c:, /config=", /config:, /c=, /c:
            string[] prefixes = { "--config=", "--config:", "-c=", "-c:", "/config=", "/config:", "/c=", "/c:" };
            foreach (var prefix in prefixes)
            {
                if (arg.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    string val = arg[prefix.Length..].Trim().Trim('\"', '\'').Trim();
                    if (!string.IsNullOrWhiteSpace(val))
                    {
                        return NormalizePath(val);
                    }
                }
            }

            // Direct .yaml or .yml file argument (skip first arg if executable)
            if (i > 0 && (arg.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase) || arg.EndsWith(".yml", StringComparison.OrdinalIgnoreCase)))
            {
                return NormalizePath(arg);
            }
        }

        return null;
    }

    public string? GetEnvironmentConfigPath()
    {
        string? env = Environment.GetEnvironmentVariable("CATALYST_CONFIG");
        if (!string.IsNullOrWhiteSpace(env))
        {
            string normalized = NormalizePath(env);
            if (File.Exists(normalized))
            {
                return normalized;
            }
        }

        return null;
    }

    public string GetDefaultConfigPath()
    {
        string baseDir = AppDomain.CurrentDomain.BaseDirectory;
        var dir = new DirectoryInfo(baseDir);
        while (dir != null)
        {
            string candidate = Path.Combine(dir.FullName, DefaultFileName);
            if (File.Exists(candidate))
            {
                return candidate;
            }
            dir = dir.Parent;
        }

        return Path.GetFullPath(Path.Combine(baseDir, DefaultFileName));
    }

    public string GetActiveConfigPath(string[]? args = null)
    {
        if (!string.IsNullOrWhiteSpace(_customConfigPath))
            return NormalizePath(_customConfigPath);

        string? cli = ParseCommandLineConfigPath(args);
        if (!string.IsNullOrWhiteSpace(cli))
            return NormalizePath(cli);

        string? env = GetEnvironmentConfigPath();
        if (!string.IsNullOrWhiteSpace(env))
            return NormalizePath(env);

        var userSettings = _settingsStorage.LoadSettings();
        if (!string.IsNullOrWhiteSpace(userSettings?.ConfigPath))
        {
            string normalized = NormalizePath(userSettings.ConfigPath);
            if (!string.IsNullOrWhiteSpace(normalized))
            {
                return normalized;
            }
        }

        return GetDefaultConfigPath();
    }

    public string GetRootDir(string? configPath = null)
    {
        string activePath = string.IsNullOrWhiteSpace(configPath) ? GetActiveConfigPath() : NormalizePath(configPath);
        try
        {
            string? dir = Path.GetDirectoryName(Path.GetFullPath(activePath));
            return string.IsNullOrWhiteSpace(dir) ? AppDomain.CurrentDomain.BaseDirectory : dir;
        }
        catch
        {
            return AppDomain.CurrentDomain.BaseDirectory;
        }
    }

    public string GetDefaultIconsDir(string? configPath = null)
    {
        return Path.Combine(GetRootDir(configPath), DefaultIconsDirName);
    }

    public string GetIconsDir(string? configPath = null)
    {
        if (!string.IsNullOrWhiteSpace(_customIconsDir))
            return NormalizeDirectoryPath(_customIconsDir);

        var userSettings = _settingsStorage.LoadSettings();
        if (!string.IsNullOrWhiteSpace(userSettings?.IconsDir))
        {
            string normalized = NormalizeDirectoryPath(userSettings.IconsDir);
            if (!string.IsNullOrWhiteSpace(normalized))
            {
                return normalized;
            }
        }

        return GetDefaultIconsDir(configPath);
    }
}
