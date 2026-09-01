using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using Catalyst.Core.Ports.Inbound;

using MediaBrush = System.Windows.Media.Brush;
using MediaColor = System.Windows.Media.Color;
using MediaColorConverter = System.Windows.Media.ColorConverter;
using MediaSolidColorBrush = System.Windows.Media.SolidColorBrush;

namespace Catalyst.Adapters.UI.ViewModels;

public class SettingsViewModel : INotifyPropertyChanged
{
    private readonly IAppConfigurationService _configService;
    private readonly IHotkeyService _hotkeyService;

    private string _configFilePath = string.Empty;
    private string _iconsDirectory = string.Empty;
    private string _configuredHotkey = "Alt+Space";
    private string _statusMessage = "Ready";
    private MediaBrush _statusBrush = new MediaSolidColorBrush((MediaColor)MediaColorConverter.ConvertFromString("#94A3B8"));

    public SettingsViewModel(
        IAppConfigurationService configService,
        IHotkeyService hotkeyService)
    {
        _configService = configService;
        _hotkeyService = hotkeyService;

        _configFilePath = _configService.ActiveConfigPath;
        _iconsDirectory = _configService.GetCustomIconsDir() ?? string.Empty;
        _configuredHotkey = _configService.ConfiguredHotkey;

        if (!string.IsNullOrWhiteSpace(_configFilePath) && File.Exists(_configFilePath))
        {
            LoadConfig(_configFilePath);
        }
    }

    public string ConfigFilePath
    {
        get => _configFilePath;
        set
        {
            if (_configFilePath != value)
            {
                _configFilePath = value;
                OnPropertyChanged();
            }
        }
    }

    public string IconsDirectory
    {
        get => _iconsDirectory;
        set
        {
            if (_iconsDirectory != value)
            {
                _iconsDirectory = value;
                OnPropertyChanged();
            }
        }
    }

    public string ConfiguredHotkey
    {
        get => _configuredHotkey;
        set
        {
            if (_configuredHotkey != value)
            {
                _configuredHotkey = value;
                OnPropertyChanged();
            }
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set
        {
            if (_statusMessage != value)
            {
                _statusMessage = value;
                OnPropertyChanged();
            }
        }
    }

    public MediaBrush StatusBrush
    {
        get => _statusBrush;
        set
        {
            if (_statusBrush != value)
            {
                _statusBrush = value;
                OnPropertyChanged();
            }
        }
    }

    public void LoadConfig(string? path = null)
    {
        string targetPath = path ?? ConfigFilePath;
        if (string.IsNullOrWhiteSpace(targetPath)) return;

        try
        {
            _configService.SetCustomConfigPath(targetPath);
            var apps = _configService.LoadApps(targetPath);
            ConfigFilePath = _configService.ActiveConfigPath;
            IconsDirectory = _configService.GetCustomIconsDir() ?? string.Empty;
            ConfiguredHotkey = _configService.ConfiguredHotkey;
            SetStatus($"Loaded {apps.Count} apps from {Path.GetFileName(ConfigFilePath)}", "#4ADE80");
        }
        catch (Exception ex)
        {
            SetStatus($"Error loading configuration: {ex.Message}", "#F87171");
        }
    }

    public void ResetToDefault()
    {
        try
        {
            _configService.SetCustomConfigPath(null);
            _configService.SetCustomIconsDir(null);
            var apps = _configService.LoadApps();
            ConfigFilePath = _configService.ActiveConfigPath;
            IconsDirectory = string.Empty;
            ConfiguredHotkey = _configService.ConfiguredHotkey;
            SetStatus("Reset configuration path to default", "#60A5FA");
        }
        catch (Exception ex)
        {
            SetStatus($"Error resetting path: {ex.Message}", "#F87171");
        }
    }

    public void ResetIconsDirectoryToDefault()
    {
        try
        {
            _configService.SetCustomIconsDir(null);
            IconsDirectory = _configService.IconsBaseDir;
            SetStatus("Reset output icon directory to default", "#60A5FA");
        }
        catch (Exception ex)
        {
            SetStatus($"Error resetting icon directory: {ex.Message}", "#F87171");
        }
    }

    public bool ValidateHotkey(string hotkey)
    {
        if (string.IsNullOrWhiteSpace(hotkey)) return false;
        return _hotkeyService.TryParseHotkey(hotkey, out _, out _);
    }

    public void Save()
    {
        try
        {
            string cleanHotkey = ConfiguredHotkey?.Trim() ?? "Alt+Space";
            if (!ValidateHotkey(cleanHotkey))
            {
                SetStatus($"Invalid hotkey format: '{cleanHotkey}'. Use e.g. Alt+Space, Ctrl+Alt+C", "#F87171");
                return;
            }

            _configService.ConfiguredHotkey = cleanHotkey;

            string? targetPath = string.IsNullOrWhiteSpace(ConfigFilePath) ? null : ConfigFilePath.Trim();
            _configService.SetCustomConfigPath(targetPath);

            string? targetIconsDir = string.IsNullOrWhiteSpace(IconsDirectory) ? null : IconsDirectory.Trim();
            _configService.SetCustomIconsDir(targetIconsDir);

            string path = string.IsNullOrWhiteSpace(targetPath) ? _configService.ActiveConfigPath : targetPath;
            var apps = _configService.LoadApps(path);
            _configService.SaveApps(apps, cleanHotkey, path);

            ConfigFilePath = _configService.ActiveConfigPath;
            IconsDirectory = _configService.IconsBaseDir;
            ConfiguredHotkey = _configService.ConfiguredHotkey;

            SetStatus("Settings saved successfully", "#4ADE80");
        }
        catch (Exception ex)
        {
            SetStatus($"Error saving settings: {ex.Message}", "#F87171");
        }
    }

    private void SetStatus(string message, string hexColor)
    {
        StatusMessage = message;
        StatusBrush = new MediaSolidColorBrush((MediaColor)MediaColorConverter.ConvertFromString(hexColor));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
