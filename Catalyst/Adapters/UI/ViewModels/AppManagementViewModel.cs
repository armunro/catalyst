using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Media.Imaging;
using Catalyst.Core.Ports.Inbound;
using Catalyst.Adapters.UI.Navigation;

namespace Catalyst.Adapters.UI.ViewModels;

public class AppManagementViewModel : INotifyPropertyChanged
{
    private readonly IAppConfigurationService _configService;
    private readonly IIconManagementService _iconService;
    private readonly IHotkeyService _hotkeyService;
    private readonly IWindowService _windowService;

    private AppInfo? _selectedApp;
    private string _configuredHotkey = "Alt+Space";
    private string _configFilePath = string.Empty;

    public AppManagementViewModel(
        IAppConfigurationService configService,
        IIconManagementService iconService,
        IHotkeyService hotkeyService,
        IWindowService windowService)
    {
        _configService = configService;
        _iconService = iconService;
        _hotkeyService = hotkeyService;
        _windowService = windowService;

        Apps = new ObservableCollection<AppInfo>();
        _configuredHotkey = _configService.ConfiguredHotkey;
        _configFilePath = _configService.ActiveConfigPath;

        Initialize();
    }

    public ObservableCollection<AppInfo> Apps { get; }

    public AppInfo? SelectedApp
    {
        get => _selectedApp;
        set
        {
            if (_selectedApp != value)
            {
                _selectedApp = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasSelectedApp));
            }
        }
    }

    public bool HasSelectedApp => SelectedApp != null;

    public string ConfiguredHotkey
    {
        get => _configuredHotkey;
        set
        {
            if (_configuredHotkey != value)
            {
                _configuredHotkey = value;
                _configService.ConfiguredHotkey = value;
                OnPropertyChanged();
            }
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
                _configService.SetCustomConfigPath(value);
                OnPropertyChanged();
                OnPropertyChanged(nameof(RootDir));
                OnPropertyChanged(nameof(IconsBaseDir));
            }
        }
    }

    public string RootDir => _configService.RootDir;

    public string IconsBaseDir => _configService.IconsBaseDir;

    public void Initialize(IEnumerable<AppInfo>? initialApps = null, string? initialHotkey = null, string? initialConfigPath = null)
    {
        if (!string.IsNullOrWhiteSpace(initialHotkey))
        {
            ConfiguredHotkey = initialHotkey;
        }

        if (!string.IsNullOrWhiteSpace(initialConfigPath))
        {
            ConfigFilePath = initialConfigPath;
        }

        Apps.Clear();
        if (initialApps != null)
        {
            foreach (var app in initialApps)
            {
                if (string.IsNullOrEmpty(app.IconPath) || !File.Exists(app.IconPath))
                {
                    var preview = _iconService.RenderPreview(app);
                    if (preview != null)
                    {
                        app.PreviewSource = preview;
                    }
                }
                Apps.Add(app);
            }
        }
        else
        {
            var loaded = _configService.LoadApps(ConfigFilePath);
            foreach (var app in loaded)
            {
                if (string.IsNullOrEmpty(app.IconPath) || !File.Exists(app.IconPath))
                {
                    var preview = _iconService.RenderPreview(app);
                    if (preview != null)
                    {
                        app.PreviewSource = preview;
                    }
                }
                Apps.Add(app);
            }
        }

        if (Apps.Count > 0)
        {
            SelectedApp = Apps[0];
        }
    }

    public AppInfo AddNewApp()
    {
        var newApp = new AppInfo
        {
            Name = "New Application",
            Color = "#007ACC",
            BackgroundType = "Solid",
            GradientDirection = "Diagonal"
        };
        Apps.Add(newApp);
        SelectedApp = newApp;
        SaveConfig();
        return newApp;
    }

    public void DeleteApp(AppInfo? app)
    {
        app ??= SelectedApp;
        if (app != null && Apps.Contains(app))
        {
            int index = Apps.IndexOf(app);
            Apps.Remove(app);
            if (Apps.Count > 0)
            {
                SelectedApp = Apps[Math.Min(index, Apps.Count - 1)];
            }
            else
            {
                SelectedApp = null;
            }
            SaveConfig();
        }
    }

    public void MoveAppUp(AppInfo? app)
    {
        app ??= SelectedApp;
        if (app == null) return;

        int index = Apps.IndexOf(app);
        if (index > 0)
        {
            Apps.Move(index, index - 1);
            SelectedApp = app;
            SaveConfig();
        }
    }

    public void MoveAppDown(AppInfo? app)
    {
        app ??= SelectedApp;
        if (app == null) return;

        int index = Apps.IndexOf(app);
        if (index >= 0 && index < Apps.Count - 1)
        {
            Apps.Move(index, index + 1);
            SelectedApp = app;
            SaveConfig();
        }
    }

    public void MoveApp(int oldIndex, int newIndex)
    {
        if (oldIndex >= 0 && newIndex >= 0 && oldIndex < Apps.Count && newIndex < Apps.Count && oldIndex != newIndex)
        {
            var item = Apps[oldIndex];
            Apps.Move(oldIndex, newIndex);
            SelectedApp = item;
            SaveConfig();
        }
    }

    public void SaveConfig()
    {
        if (!string.IsNullOrWhiteSpace(ConfigFilePath))
        {
            _configService.SetCustomConfigPath(ConfigFilePath);
        }
        _configService.SaveApps(Apps, ConfiguredHotkey, ConfigFilePath);
    }

    public BitmapSource? RenderPreview(AppInfo app)
    {
        return _iconService.RenderPreview(app);
    }

    public void GenerateIcon(AppInfo app, Action<string>? logger = null)
    {
        _iconService.GenerateIcon(app, logger);
    }

    public void GenerateAllIcons(Action<string>? logger = null)
    {
        _iconService.GenerateAllIcons(Apps, logger);
    }

    public bool UpdateProjectFavicon(AppInfo? app = null, Action<string>? logger = null)
    {
        app ??= SelectedApp;
        if (app == null) return false;
        return _iconService.UpdateProjectFavicon(app, logger);
    }

    public void ShowIconsResult(System.Windows.Window? owner = null)
    {
        string iconsDir = _configService.IconsBaseDir;
        var list = new List<(string AppName, string IconPath)>();
        foreach (var app in Apps)
        {
            string png = Path.Combine(iconsDir, app.Name, $"{app.Name}.png");
            list.Add((app.Name, png));
        }
        _windowService.ShowIconsResult(iconsDir, list, owner);
    }

    public bool ValidateHotkey(string hotkey)
    {
        return _hotkeyService.TryParseHotkey(hotkey, out _, out _);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
