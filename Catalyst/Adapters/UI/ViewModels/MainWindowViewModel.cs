using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Data;
using Catalyst.Core.Ports.Inbound;
using Catalyst.Adapters.UI.Navigation;

namespace Catalyst.Adapters.UI.ViewModels;

public class MainWindowViewModel : INotifyPropertyChanged
{
    private readonly IAppConfigurationService _configService;
    private readonly IAppLauncherService _launcherService;
    private readonly IHotkeyService _hotkeyService;
    private readonly IWindowService _windowService;
    private readonly IIconManagementService? _iconService;

    private string _searchText = string.Empty;
    private string _statusText = string.Empty;
    private System.Windows.Media.Brush _statusBrush = System.Windows.Media.Brushes.Gray;
    private bool _canStartAll;
    private bool _canStopAll;
    private ListCollectionView? _appsView;

    public MainWindowViewModel(
        IAppConfigurationService configService,
        IAppLauncherService launcherService,
        IHotkeyService hotkeyService,
        IWindowService windowService,
        IIconManagementService? iconService = null)
    {
        _configService = configService;
        _launcherService = launcherService;
        _hotkeyService = hotkeyService;
        _windowService = windowService;
        _iconService = iconService;

        Apps = new ObservableCollection<AppInfo>();
        SetupCollectionView();
    }

    public ObservableCollection<AppInfo> Apps { get; }

    public ListCollectionView AppsView => _appsView ??= SetupCollectionView();

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (_searchText != value)
            {
                _searchText = value;
                OnPropertyChanged();
                RefreshFilterAndShortcuts();
            }
        }
    }

    public string StatusText
    {
        get => _statusText;
        private set { _statusText = value; OnPropertyChanged(); }
    }

    public System.Windows.Media.Brush StatusBrush
    {
        get => _statusBrush;
        private set { _statusBrush = value; OnPropertyChanged(); }
    }

    public bool CanStartAll
    {
        get => _canStartAll;
        private set { _canStartAll = value; OnPropertyChanged(); }
    }

    public bool CanStopAll
    {
        get => _canStopAll;
        private set { _canStopAll = value; OnPropertyChanged(); }
    }

    public string ConfiguredHotkey
    {
        get => _configService.ConfiguredHotkey;
        set => _configService.ConfiguredHotkey = value;
    }

    public string ActiveConfigPath => _configService.ActiveConfigPath;

    public void LoadApps(string? customPath = null)
    {
        Apps.Clear();
        var loaded = _configService.LoadApps(customPath);
        foreach (var app in loaded)
        {
            app.PropertyChanged += App_PropertyChanged;
            Apps.Add(app);
        }

        RefreshFilterAndShortcuts();
        _launcherService.DetectRunningApps(Apps);
        UpdateStatus();
    }

    public async Task StartAppAsync(AppInfo app)
    {
        await _launcherService.LaunchAppAsync(app);
        UpdateStatus();
    }

    public async Task StopAppAsync(AppInfo app)
    {
        await _launcherService.StopAppAsync(app);
        UpdateStatus();
    }

    public async Task ToggleAppAsync(AppInfo app)
    {
        if (app.IsRunning)
        {
            await StopAppAsync(app);
        }
        else if (app.IsLaunchable)
        {
            await StartAppAsync(app);
        }
    }

    public async Task StartAllAsync()
    {
        await _launcherService.StartAllAsync(Apps);
        UpdateStatus();
    }

    public async Task StopAllAsync()
    {
        await _launcherService.StopAllAsync(Apps);
        UpdateStatus();
    }

    public void MoveAppUp(AppInfo app)
    {
        if (app == null) return;
        var visibleApps = AppsView.Cast<AppInfo>().ToList();
        int visibleIndex = visibleApps.IndexOf(app);
        if (visibleIndex > 0)
        {
            var targetApp = visibleApps[visibleIndex - 1];
            int oldIndex = Apps.IndexOf(app);
            int newIndex = Apps.IndexOf(targetApp);
            if (oldIndex >= 0 && newIndex >= 0 && oldIndex != newIndex)
            {
                Apps.Move(oldIndex, newIndex);
                SaveConfig();
                RefreshFilterAndShortcuts();
            }
        }
    }

    public void MoveAppDown(AppInfo app)
    {
        if (app == null) return;
        var visibleApps = AppsView.Cast<AppInfo>().ToList();
        int visibleIndex = visibleApps.IndexOf(app);
        if (visibleIndex >= 0 && visibleIndex < visibleApps.Count - 1)
        {
            var targetApp = visibleApps[visibleIndex + 1];
            int oldIndex = Apps.IndexOf(app);
            int newIndex = Apps.IndexOf(targetApp);
            if (oldIndex >= 0 && newIndex >= 0 && oldIndex != newIndex)
            {
                Apps.Move(oldIndex, newIndex);
                SaveConfig();
                RefreshFilterAndShortcuts();
            }
        }
    }

    public void MoveApp(int oldIndex, int newIndex)
    {
        if (oldIndex >= 0 && newIndex >= 0 && oldIndex < Apps.Count && newIndex < Apps.Count && oldIndex != newIndex)
        {
            Apps.Move(oldIndex, newIndex);
            SaveConfig();
            RefreshFilterAndShortcuts();
        }
    }

    public void SaveConfig()
    {
        _configService.SaveApps(Apps);
    }

    public void OpenFolder(AppInfo app)
    {
        _launcherService.OpenAppLocation(app);
    }

    public void OpenLogViewer(AppInfo app, System.Windows.Window? owner = null)
    {
        _windowService.ShowLogViewer(app, owner);
    }

    public void OpenAppManagement(System.Windows.Window? owner = null)
    {
        _windowService.ShowAppManagement(owner);
        LoadApps();
    }

    public void OpenSettings(System.Windows.Window? owner = null)
    {
        _windowService.ShowSettings(owner);
        LoadApps();
    }

    public void RefreshFilterAndShortcuts()
    {
        AppsView.Refresh();

        int index = 1;
        foreach (var item in AppsView)
        {
            if (item is AppInfo app)
            {
                if (index <= 9)
                {
                    app.ShortcutIndex = index.ToString();
                    index++;
                }
                else
                {
                    app.ShortcutIndex = string.Empty;
                }
            }
        }

        UpdateStatus();
    }

    public void UpdateStatus()
    {
        int runningCount = Apps.Count(a => !a.IsHidden && a.IsRunning);
        int totalAppsCount = Apps.Count(a => !a.IsHidden && a.IsLaunchable);
        StatusText = $"{runningCount} running • {totalAppsCount} total";

        StatusBrush = runningCount > 0
            ? new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#4ADE80"))
            : new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#94A3B8"));

        CanStartAll = Apps.Any(a => !a.IsHidden && a.IsLaunchable && !a.IsRunning);
        CanStopAll = runningCount > 0;
    }

    private ListCollectionView SetupCollectionView()
    {
        _appsView = new ListCollectionView(Apps)
        {
            Filter = FilterApps
        };
        return _appsView;
    }

    private bool FilterApps(object item)
    {
        if (item is not AppInfo app) return false;
        if (app.IsHidden) return false;

        string query = SearchText?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(query)) return true;

        if (app.Name.Contains(query, StringComparison.OrdinalIgnoreCase)) return true;
        if (app.Label.Contains(query, StringComparison.OrdinalIgnoreCase)) return true;
        if (app.ShortLaunchTarget.Contains(query, StringComparison.OrdinalIgnoreCase)) return true;
        if (app.LaunchTarget.Contains(query, StringComparison.OrdinalIgnoreCase)) return true;
        if (app.LaunchTypeDescription.Contains(query, StringComparison.OrdinalIgnoreCase)) return true;
        if (app.BootstrapIcon.Contains(query, StringComparison.OrdinalIgnoreCase)) return true;
        if (app.IsRunningString.Contains(query, StringComparison.OrdinalIgnoreCase)) return true;

        return false;
    }

    private void App_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(AppInfo.IsRunning) or nameof(AppInfo.IsHidden) or nameof(AppInfo.Process))
        {
            UpdateStatus();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
