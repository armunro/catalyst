using System;
using System.IO;
using System.Text.Json;
using Catalyst.Core.Domain.Models;
using Catalyst.Core.Ports.Outbound;

namespace Catalyst.Adapters.Persistence;

public class JsonSettingsStorage : ISettingsStorage
{
    private static readonly string DefaultSettingsDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Catalyst");

    private static readonly string DefaultSettingsFilePath = Path.Combine(DefaultSettingsDirectory, "settings.json");

    private string _settingsFilePath = DefaultSettingsFilePath;

    public string SettingsFilePath
    {
        get => _settingsFilePath;
        set => _settingsFilePath = value;
    }

    public UserSettings? LoadSettings()
    {
        try
        {
            if (!File.Exists(SettingsFilePath))
                return null;

            string json = File.ReadAllText(SettingsFilePath);
            return JsonSerializer.Deserialize<UserSettings>(json);
        }
        catch
        {
            return null;
        }
    }

    public void SaveSettings(UserSettings settings)
    {
        try
        {
            string? dir = Path.GetDirectoryName(SettingsFilePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            string json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SettingsFilePath, json);
        }
        catch
        {
            // Ignore settings save errors gracefully
        }
    }
}
