using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Catalyst.Services;

public static class ConfigService
{
    public const string DefaultFileName = "apps.yaml";

    public static string SettingsFilePath { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Catalyst",
        "settings.json");

    public static string? CustomConfigPath { get; set; }

    public static string? ParseCommandLineConfigPath(string[]? args = null)
    {
        args ??= Environment.GetCommandLineArgs();
        if (args == null || args.Length <= 1) return null;

        for (int i = 1; i < args.Length; i++)
        {
            string arg = args[i];
            if (string.IsNullOrWhiteSpace(arg)) continue;

            if (arg.Equals("--config", StringComparison.OrdinalIgnoreCase) ||
                arg.Equals("-c", StringComparison.OrdinalIgnoreCase) ||
                arg.Equals("/config", StringComparison.OrdinalIgnoreCase))
            {
                if (i + 1 < args.Length && !string.IsNullOrWhiteSpace(args[i + 1]))
                {
                    return args[i + 1].Trim('"', '\'');
                }
            }
            else if (arg.StartsWith("--config=", StringComparison.OrdinalIgnoreCase))
            {
                return arg.Substring("--config=".Length).Trim('"', '\'');
            }
            else if (arg.StartsWith("/config:", StringComparison.OrdinalIgnoreCase))
            {
                return arg.Substring("/config:".Length).Trim('"', '\'');
            }
            else if (arg.StartsWith("-c:", StringComparison.OrdinalIgnoreCase))
            {
                return arg.Substring("-c:".Length).Trim('"', '\'');
            }
            else if (arg.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase) ||
                     arg.EndsWith(".yml", StringComparison.OrdinalIgnoreCase))
            {
                return arg.Trim('"', '\'');
            }
        }

        return null;
    }

    public static string? GetEnvironmentConfigPath()
    {
        string? env = Environment.GetEnvironmentVariable("CATALYST_CONFIG");
        if (!string.IsNullOrWhiteSpace(env)) return env.Trim('"', '\'');

        env = Environment.GetEnvironmentVariable("CATALYST_APPS_YAML");
        if (!string.IsNullOrWhiteSpace(env)) return env.Trim('"', '\'');

        env = Environment.GetEnvironmentVariable("CATALYST_CONFIG_PATH");
        if (!string.IsNullOrWhiteSpace(env)) return env.Trim('"', '\'');

        return null;
    }

    public static string? GetUserConfigPath()
    {
        try
        {
            if (File.Exists(SettingsFilePath))
            {
                string json = File.ReadAllText(SettingsFilePath);
                var settings = JsonSerializer.Deserialize<UserSettings>(json);
                if (!string.IsNullOrWhiteSpace(settings?.ConfigPath))
                {
                    return settings.ConfigPath;
                }
            }
        }
        catch
        {
            // Ignore settings read errors
        }
        return null;
    }

    public static void SetUserConfigPath(string? path)
    {
        try
        {
            string? dir = Path.GetDirectoryName(SettingsFilePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var settings = new UserSettings { ConfigPath = path };
            string json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SettingsFilePath, json);
        }
        catch
        {
            // Ignore settings write errors
        }
    }

    public static string GetDefaultConfigPath()
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

    public static string GetActiveConfigPath(string[]? args = null)
    {
        if (!string.IsNullOrWhiteSpace(CustomConfigPath))
        {
            return Path.GetFullPath(CustomConfigPath);
        }

        string? cli = ParseCommandLineConfigPath(args);
        if (!string.IsNullOrWhiteSpace(cli))
        {
            return Path.GetFullPath(cli);
        }

        string? env = GetEnvironmentConfigPath();
        if (!string.IsNullOrWhiteSpace(env))
        {
            return Path.GetFullPath(env);
        }

        string? userPath = GetUserConfigPath();
        if (!string.IsNullOrWhiteSpace(userPath))
        {
            return Path.GetFullPath(userPath);
        }

        return GetDefaultConfigPath();
    }

    public static string GetRootDir(string? configPath = null)
    {
        string activePath = string.IsNullOrWhiteSpace(configPath) ? GetActiveConfigPath() : configPath;
        string? dir = Path.GetDirectoryName(Path.GetFullPath(activePath));
        return string.IsNullOrWhiteSpace(dir) ? AppDomain.CurrentDomain.BaseDirectory : dir;
    }

    public static AppConfigFile? LoadConfigFile(string configPath)
    {
        if (!File.Exists(configPath)) return null;

        var yaml = File.ReadAllText(configPath);
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();

        return deserializer.Deserialize<AppConfigFile>(yaml);
    }

    public static void SaveConfigFile(AppConfigFile config, string configPath)
    {
        string? dir = Path.GetDirectoryName(configPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        var serializer = new SerializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();

        string yaml = serializer.Serialize(config);
        File.WriteAllText(configPath, yaml);
    }
}

public class UserSettings
{
    public string? ConfigPath { get; set; }
}
