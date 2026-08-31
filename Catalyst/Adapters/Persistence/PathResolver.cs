using System;
using System.IO;
using Catalyst.Core.Ports.Outbound;

namespace Catalyst.Adapters.Persistence;

public class PathResolver : IPathResolver
{
    private const string DefaultFileName = "apps.yaml";
    private readonly ISettingsStorage _settingsStorage;

    public PathResolver(ISettingsStorage settingsStorage)
    {
        _settingsStorage = settingsStorage;
    }

    public string? CustomConfigPath { get; set; }

    public string? ParseCommandLineConfigPath(string[]? args = null)
    {
        args ??= Environment.GetCommandLineArgs();
        if (args == null || args.Length == 0) return null;

        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i].Trim();

            // Match flags: --config, -c, /config, /c
            if (arg.Equals("--config", StringComparison.OrdinalIgnoreCase) ||
                arg.Equals("-c", StringComparison.OrdinalIgnoreCase) ||
                arg.Equals("/config", StringComparison.OrdinalIgnoreCase) ||
                arg.Equals("/c", StringComparison.OrdinalIgnoreCase))
            {
                if (i + 1 < args.Length && !string.IsNullOrWhiteSpace(args[i + 1]))
                {
                    return args[i + 1].Trim();
                }
            }

            // Match prefixes: --config=, --config:, -c=, -c:, /config=, /config:, /c=, /c:
            string[] prefixes = { "--config=", "--config:", "-c=", "-c:", "/config=", "/config:", "/c=", "/c:" };
            foreach (var prefix in prefixes)
            {
                if (arg.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    string val = arg[prefix.Length..].Trim();
                    if (!string.IsNullOrWhiteSpace(val))
                    {
                        return val;
                    }
                }
            }

            // Direct .yaml or .yml file argument (skip first arg if executable)
            if (i > 0 && (arg.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase) || arg.EndsWith(".yml", StringComparison.OrdinalIgnoreCase)))
            {
                return arg;
            }
        }

        return null;
    }

    public string? GetEnvironmentConfigPath()
    {
        string? env = Environment.GetEnvironmentVariable("CATALYST_CONFIG");
        if (!string.IsNullOrWhiteSpace(env) && File.Exists(env))
        {
            return Path.GetFullPath(env);
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
        if (!string.IsNullOrWhiteSpace(CustomConfigPath))
            return Path.GetFullPath(CustomConfigPath);

        string? cli = ParseCommandLineConfigPath(args);
        if (!string.IsNullOrWhiteSpace(cli))
            return Path.GetFullPath(cli);

        string? env = GetEnvironmentConfigPath();
        if (!string.IsNullOrWhiteSpace(env))
            return Path.GetFullPath(env);

        var userSettings = _settingsStorage.LoadSettings();
        if (!string.IsNullOrWhiteSpace(userSettings?.ConfigPath))
        {
            return Path.GetFullPath(userSettings.ConfigPath);
        }

        return GetDefaultConfigPath();
    }

    public string GetRootDir(string? configPath = null)
    {
        string activePath = string.IsNullOrWhiteSpace(configPath) ? GetActiveConfigPath() : configPath;
        string? dir = Path.GetDirectoryName(Path.GetFullPath(activePath));
        return string.IsNullOrWhiteSpace(dir) ? AppDomain.CurrentDomain.BaseDirectory : dir;
    }
}
