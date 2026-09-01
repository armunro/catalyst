using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using Catalyst.Core.Domain.Models;
using Catalyst.Core.Ports.Outbound;

namespace Catalyst.Adapters.Persistence;

public class JsonSettingsStorage : ISettingsStorage
{
    private static readonly string RoamingSettingsDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Catalyst");

    private static readonly string LocalSettingsDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Catalyst");

    private static readonly string DefaultSettingsFilePath = Path.Combine(RoamingSettingsDirectory, "settings.json");
    private static readonly string LocalSettingsFilePath = Path.Combine(LocalSettingsDirectory, "settings.json");

    private string _settingsFilePath;

    public JsonSettingsStorage(string? settingsFilePath = null)
    {
        _settingsFilePath = string.IsNullOrWhiteSpace(settingsFilePath) ? DefaultSettingsFilePath : settingsFilePath;
    }

    public string SettingsFilePath
    {
        get => _settingsFilePath;
        set => _settingsFilePath = value;
    }

    public UserSettings? LoadSettings()
    {
        // 1. Try explicit / primary settings path
        var settings = TryLoadFromPath(SettingsFilePath);
        if (settings != null && (!string.IsNullOrWhiteSpace(settings.ConfigPath) || !string.IsNullOrWhiteSpace(settings.IconsDir) || !string.IsNullOrWhiteSpace(settings.Hotkey)))
        {
            return settings;
        }

        // 2. If using the default settings file path, check fallback candidate locations
        if (string.Equals(SettingsFilePath, DefaultSettingsFilePath, StringComparison.OrdinalIgnoreCase))
        {
            string[] fallbackPaths =
            {
                LocalSettingsFilePath,
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".catalyst", "settings.json"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.json")
            };

            foreach (var fallback in fallbackPaths)
            {
                var candidate = TryLoadFromPath(fallback);
                if (candidate != null && (!string.IsNullOrWhiteSpace(candidate.ConfigPath) || !string.IsNullOrWhiteSpace(candidate.IconsDir) || !string.IsNullOrWhiteSpace(candidate.Hotkey)))
                {
                    return candidate;
                }
            }
        }

        return settings;
    }

    private static UserSettings? TryLoadFromPath(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            return null;

        for (int i = 0; i < 3; i++)
        {
            try
            {
                using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var reader = new StreamReader(stream);
                string json = reader.ReadToEnd();

                if (string.IsNullOrWhiteSpace(json))
                    return null;

                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    AllowTrailingCommas = true,
                    ReadCommentHandling = JsonCommentHandling.Skip
                };

                var userSettings = JsonSerializer.Deserialize<UserSettings>(json, options) ?? new UserSettings();

                // Flexible extraction for alternate naming conventions
                if (string.IsNullOrWhiteSpace(userSettings.ConfigPath) || string.IsNullOrWhiteSpace(userSettings.IconsDir) || string.IsNullOrWhiteSpace(userSettings.Hotkey))
                {
                    try
                    {
                        var docOptions = new JsonDocumentOptions
                        {
                            CommentHandling = JsonCommentHandling.Skip,
                            AllowTrailingCommas = true
                        };
                        using var doc = JsonDocument.Parse(json, docOptions);
                        var root = doc.RootElement;

                        if (string.IsNullOrWhiteSpace(userSettings.ConfigPath))
                        {
                            string[] configKeys = { "configPath", "config_path", "configFilePath", "config_file_path", "configFile", "config_file", "configurationPath", "configuration_path", "path", "config" };
                            foreach (var key in configKeys)
                            {
                                if (TryGetPropertyCaseInsensitive(root, key, out var val) && val.ValueKind == JsonValueKind.String)
                                {
                                    string? str = val.GetString();
                                    if (!string.IsNullOrWhiteSpace(str))
                                    {
                                        userSettings.ConfigPath = str;
                                        break;
                                    }
                                }
                            }
                        }

                        if (string.IsNullOrWhiteSpace(userSettings.IconsDir))
                        {
                            string[] iconKeys = { "iconsDir", "icons_dir", "iconsDirectory", "icons_directory", "iconDirectory", "icon_directory", "iconDir", "icon_dir", "iconsPath", "icons_path", "outputIconDirectory", "output_icon_directory" };
                            foreach (var key in iconKeys)
                            {
                                if (TryGetPropertyCaseInsensitive(root, key, out var val) && val.ValueKind == JsonValueKind.String)
                                {
                                    string? str = val.GetString();
                                    if (!string.IsNullOrWhiteSpace(str))
                                    {
                                        userSettings.IconsDir = str;
                                        break;
                                    }
                                }
                            }
                        }

                        if (string.IsNullOrWhiteSpace(userSettings.Hotkey))
                        {
                            string[] hotkeyKeys = { "hotkey", "hot_key", "globalHotkey", "global_hotkey", "shortcut", "globalShortcut" };
                            foreach (var key in hotkeyKeys)
                            {
                                if (TryGetPropertyCaseInsensitive(root, key, out var val) && val.ValueKind == JsonValueKind.String)
                                {
                                    string? str = val.GetString();
                                    if (!string.IsNullOrWhiteSpace(str))
                                    {
                                        userSettings.Hotkey = str;
                                        break;
                                    }
                                }
                            }
                        }
                    }
                    catch { }
                }

                return userSettings;
            }
            catch (IOException) when (i < 2)
            {
                Thread.Sleep(30);
            }
            catch
            {
                return null;
            }
        }

        return null;
    }

    private static bool TryGetPropertyCaseInsensitive(JsonElement element, string name, out JsonElement value)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in element.EnumerateObject())
            {
                if (string.Equals(prop.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    value = prop.Value;
                    return true;
                }
            }
        }

        value = default;
        return false;
    }

    public void SaveSettings(UserSettings settings)
    {
        WriteSettingsToPath(SettingsFilePath, settings);

        // If saving to default roaming path, also mirror to local app data for redundancy
        if (string.Equals(SettingsFilePath, DefaultSettingsFilePath, StringComparison.OrdinalIgnoreCase))
        {
            WriteSettingsToPath(LocalSettingsFilePath, settings);
        }
    }

    private static void WriteSettingsToPath(string filePath, UserSettings settings)
    {
        if (string.IsNullOrWhiteSpace(filePath)) return;

        for (int i = 0; i < 3; i++)
        {
            try
            {
                string? dir = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };
                string json = JsonSerializer.Serialize(settings, options);

                using var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite);
                using var writer = new StreamWriter(stream);
                writer.Write(json);
                writer.Flush();
                break;
            }
            catch (IOException) when (i < 2)
            {
                Thread.Sleep(30);
            }
            catch
            {
                // Ignore settings save errors gracefully
                break;
            }
        }
    }
}
