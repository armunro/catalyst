using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using Catalyst.Services;
using Wpf.Ui.Controls;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Catalyst.Windows;

public partial class MainWindow : FluentWindow
{
    private const int SW_RESTORE = 9;
    private const int SW_SHOW = 5;

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    private readonly ObservableCollection<AppInfo> _apps = new();
    private ICollectionView? _appsView;
    private readonly Dictionary<string, LogViewerWindow> _logWindows = new();
    private NotifyIcon? _notifyIcon;
    private HotkeyManager? _hotkeyManager;
    private string _configuredHotkey = "Alt+Space";
    private string? _configFilePath;
    private bool _isExiting = false;

    public MainWindow()
    {
        InitializeComponent();
        
        _appsView = CollectionViewSource.GetDefaultView(_apps);
        _appsView.Filter = FilterApps;

        LstApps.ItemsSource = _appsView;
        this.Loaded += MainWindow_Loaded;
        this.Closing += MainWindow_Closing;
        this.Closed += MainWindow_Closed;

        LoadApps();
        SetupTrayIcon();
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        SetupHotkey();
        DetectRunningApps();
        PositionWindowAtBottomRight();
        TxtSearch.Focus();
    }

    private void SetupHotkey()
    {
        _hotkeyManager?.Dispose();
        _hotkeyManager = new HotkeyManager();
        if (_hotkeyManager.Register(this, _configuredHotkey, OnGlobalHotkeyTriggered, out string registeredHotkey))
        {
            LblHotkeyHint.Text = $" • Hotkey: {registeredHotkey}";
        }
        else
        {
            LblHotkeyHint.Text = " • Hotkey: Failed";
        }
    }

    private void OnGlobalHotkeyTriggered()
    {
        Dispatcher.Invoke(() =>
        {
            if (this.IsVisible && this.IsActive && this.WindowState != WindowState.Minimized)
            {
                this.Hide();
            }
            else
            {
                RestoreWindow();
            }
        });
    }

    private void PositionWindowAtBottomRight()
    {
        var primaryScreen = Screen.PrimaryScreen;
        if (primaryScreen != null)
        {
            var workingArea = primaryScreen.WorkingArea;
            this.Left = workingArea.Right - this.Width - 14;
            this.Top = workingArea.Bottom - this.Height - 14;
        }
    }

    private void SetupTrayIcon()
    {
        _notifyIcon = new NotifyIcon();
        string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "favicon.ico");
        if (File.Exists(iconPath))
        {
            _notifyIcon.Icon = new System.Drawing.Icon(iconPath);
        }
        else
        {
            string projectIconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "favicon.ico");
            if (File.Exists(projectIconPath))
            {
                _notifyIcon.Icon = new System.Drawing.Icon(projectIconPath);
            }
        }
        _notifyIcon.Visible = true;
        _notifyIcon.Text = "Catalyst Launcher";
        _notifyIcon.DoubleClick += (s, e) => RestoreWindow();

        var contextMenu = new ContextMenuStrip();
        contextMenu.Items.Add("Open Catalyst", null, (s, e) => RestoreWindow());
        contextMenu.Items.Add("Manage Apps", null, (s, e) => OpenAppManagement());
        contextMenu.Items.Add("Reload Apps", null, (s, e) => LoadApps());
        contextMenu.Items.Add(new ToolStripSeparator());
        contextMenu.Items.Add("Detach and Exit", null, (s, e) => DetachAndExit());
        contextMenu.Items.Add("Exit", null, (s, e) => ExitApplication());

        _notifyIcon.ContextMenuStrip = contextMenu;
    }

    private void MainWindow_Closing(object? sender, CancelEventArgs e)
    {
        if (!_isExiting)
        {
            e.Cancel = true;
            this.Hide();
            this.ShowInTaskbar = false;
        }
    }

    private void MainWindow_Closed(object? sender, EventArgs e)
    {
        _hotkeyManager?.Dispose();
        _notifyIcon?.Dispose();
    }

    private void RestoreWindow()
    {
        this.Show();
        this.ShowInTaskbar = true;
        this.WindowState = WindowState.Normal;
        PositionWindowAtBottomRight();

        var helper = new WindowInteropHelper(this);
        ShowWindow(helper.Handle, SW_RESTORE);
        SetForegroundWindow(helper.Handle);

        this.Activate();
        TxtSearch.Focus();
        TxtSearch.SelectAll();
    }

    private void ExitApplication()
    {
        _isExiting = true;
        foreach (var app in _apps)
        {
            StopApp(app);
        }
        _notifyIcon?.Dispose();
        _hotkeyManager?.Dispose();
        System.Windows.Application.Current.Shutdown();
    }

    private void DetachAndExit()
    {
        _isExiting = true;
        _notifyIcon?.Dispose();
        _hotkeyManager?.Dispose();
        System.Windows.Application.Current.Shutdown();
    }

    private void DetectRunningApps()
    {
        foreach (var app in _apps)
        {
            if (!app.IsLaunchable) continue;
            
            string target = app.LaunchTarget;
            string processName = Path.GetFileNameWithoutExtension(target);
            if (string.IsNullOrWhiteSpace(processName)) continue;

            var existingProcesses = Process.GetProcessesByName(processName);
            if (existingProcesses.Length > 0)
            {
                var process = existingProcesses[0];
                app.Process = process;
                
                Task.Run(() =>
                {
                    try
                    {
                        process.EnableRaisingEvents = true;
                        process.Exited += (s, e) => 
                        {
                            Dispatcher.Invoke(() => {
                                app.Process = null;
                                UpdateStatus();
                            });
                        };
                        
                        if (process.HasExited)
                        {
                            Dispatcher.Invoke(() => app.Process = null);
                        }
                    }
                    catch { }
                    Dispatcher.Invoke(() => UpdateStatus());
                });
            }
        }
        UpdateStatus();
    }

    public void LoadApps(string? customPath = null)
    {
        if (!string.IsNullOrWhiteSpace(customPath))
        {
            _configFilePath = customPath;
        }
        else if (string.IsNullOrWhiteSpace(_configFilePath))
        {
            _configFilePath = ConfigService.GetActiveConfigPath();
        }

        string configPath = _configFilePath;
        string rootDir = ConfigService.GetRootDir(configPath);

        if (!File.Exists(configPath))
        {
            try
            {
                Directory.CreateDirectory(Path.Combine(rootDir, "Logs"));
            }
            catch { }
            return;
        }

        try
        {
            var config = ConfigService.LoadConfigFile(configPath);
            if (config != null)
            {
                if (!string.IsNullOrWhiteSpace(config.Hotkey))
                {
                    _configuredHotkey = config.Hotkey;
                    if (IsLoaded) SetupHotkey();
                }

                var existingProcessMap = _apps
                    .Where(a => a.Process != null && !a.Process.HasExited)
                    .ToDictionary(a => a.Name, a => a.Process);

                _apps.Clear();
                foreach (var entry in config.Apps)
                {
                    string fullProjectPath = string.Empty;
                    string fullExecPath = string.Empty;

                    if (entry.Launch != null)
                    {
                        if (!string.IsNullOrEmpty(entry.Launch.ProjectPath))
                        {
                            fullProjectPath = Path.IsPathRooted(entry.Launch.ProjectPath)
                                ? entry.Launch.ProjectPath
                                : Path.GetFullPath(Path.Combine(rootDir, entry.Launch.ProjectPath));
                        }

                        if (!string.IsNullOrEmpty(entry.Launch.ExecutablePath))
                        {
                            fullExecPath = Path.IsPathRooted(entry.Launch.ExecutablePath)
                                ? entry.Launch.ExecutablePath
                                : Path.GetFullPath(Path.Combine(rootDir, entry.Launch.ExecutablePath));
                        }
                    }

                    var appInfo = new AppInfo
                    {
                        Name = entry.Name,
                        ProjectPath = fullProjectPath,
                        ExecutablePath = fullExecPath,
                        Arguments = entry.Launch?.Arguments ?? string.Empty,
                        WorkingDirectory = entry.Launch?.WorkingDirectory ?? string.Empty,
                        RunAsAdmin = entry.Launch?.RunAsAdmin ?? false
                    };

                    if (existingProcessMap.TryGetValue(entry.Name, out var runningProc))
                    {
                        appInfo.Process = runningProc;
                    }

                    if (entry.Icon != null)
                    {
                        appInfo.Color = entry.Icon.Color;
                        appInfo.SecondaryColor = entry.Icon.SecondaryColor;
                        appInfo.BackgroundType = entry.Icon.BackgroundType;
                        appInfo.GradientDirection = entry.Icon.GradientDirection;
                        appInfo.Label = entry.Icon.Label;
                        appInfo.FaviconPath = entry.Icon.FaviconPath;
                        appInfo.BootstrapIcon = entry.Icon.BootstrapIcon;
                        appInfo.CustomGlyphSvg = entry.Icon.CustomGlyphSvg;
                        appInfo.CustomGlyphColor = entry.Icon.CustomGlyphColor;
                        appInfo.SvgOverride = entry.Icon.SvgOverride;

                        if (!string.IsNullOrEmpty(entry.Icon.IconPath))
                        {
                            appInfo.IconPath = Path.IsPathRooted(entry.Icon.IconPath)
                                ? entry.Icon.IconPath
                                : Path.GetFullPath(Path.Combine(rootDir, entry.Icon.IconPath));
                        }
                    }

                    if (string.IsNullOrEmpty(appInfo.IconPath))
                    {
                        string autoIconPath = Path.Combine(rootDir, "icons", entry.Name, $"{entry.Name}.png");
                        if (File.Exists(autoIconPath))
                        {
                            appInfo.IconPath = autoIconPath;
                        }
                    }

                    _apps.Add(appInfo);
                }
            }
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"Error loading apps: {ex.Message}");
        }

        try
        {
            Directory.CreateDirectory(Path.Combine(rootDir, "Logs"));
        }
        catch { }

        RefreshShortcutsAndFilter();
        DetectRunningApps();
    }

    private bool FilterApps(object item)
    {
        if (item is not AppInfo app) return false;

        string query = TxtSearch?.Text?.Trim() ?? string.Empty;
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

    private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
    {
        RefreshShortcutsAndFilter();
    }

    private void RefreshShortcutsAndFilter()
    {
        _appsView?.Refresh();

        // Assign numbers 1..9 to the current visible items
        int index = 1;
        if (_appsView != null)
        {
            foreach (var item in _appsView)
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
        }

        UpdateStatus();
    }

    private void TxtSearch_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Down)
        {
            if (LstApps.Items.Count > 0)
            {
                LstApps.Focus();
                if (LstApps.SelectedIndex < 0)
                {
                    LstApps.SelectedIndex = 0;
                }
                e.Handled = true;
            }
        }
        else if (e.Key == Key.Enter)
        {
            var target = LstApps.SelectedItem as AppInfo ?? (_appsView?.Cast<AppInfo>().FirstOrDefault());
            if (target != null)
            {
                ToggleApp(target);
                e.Handled = true;
            }
        }
        else if (e.Key == Key.Escape)
        {
            if (!string.IsNullOrEmpty(TxtSearch.Text))
            {
                TxtSearch.Text = string.Empty;
            }
            else
            {
                this.Hide();
            }
            e.Handled = true;
        }
    }

    private void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        // Global escape hides to tray
        if (e.Key == Key.Escape)
        {
            if (!string.IsNullOrEmpty(TxtSearch.Text))
            {
                TxtSearch.Text = string.Empty;
                TxtSearch.Focus();
            }
            else
            {
                this.Hide();
            }
            e.Handled = true;
            return;
        }

        // Ctrl+F -> focus search
        if (e.Key == Key.F && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
        {
            TxtSearch.Focus();
            TxtSearch.SelectAll();
            e.Handled = true;
            return;
        }

        // Ctrl+M -> manage apps
        if (e.Key == Key.M && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
        {
            OpenAppManagement();
            e.Handled = true;
            return;
        }

        // F5 -> reload
        if (e.Key == Key.F5)
        {
            LoadApps();
            e.Handled = true;
            return;
        }

        // Ctrl+L -> view logs
        if (e.Key == Key.L && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
        {
            if (LstApps.SelectedItem is AppInfo selectedApp)
            {
                OpenLogWindow(selectedApp);
                e.Handled = true;
                return;
            }
        }

        // 1-9 shortcuts when not actively typing search or if Alt is pressed
        bool isAltPressed = (Keyboard.Modifiers & ModifierKeys.Alt) == ModifierKeys.Alt;
        bool isSearchFocused = TxtSearch.IsFocused;

        if (!isSearchFocused || isAltPressed)
        {
            int digit = -1;
            if (e.Key >= Key.D1 && e.Key <= Key.D9)
            {
                digit = (int)e.Key - (int)Key.D1 + 1;
            }
            else if (e.Key >= Key.NumPad1 && e.Key <= Key.NumPad9)
            {
                digit = (int)e.Key - (int)Key.NumPad1 + 1;
            }

            if (digit >= 1 && digit <= 9)
            {
                var targetApp = _appsView?.Cast<AppInfo>().FirstOrDefault(a => a.ShortcutIndex == digit.ToString());
                if (targetApp != null)
                {
                    LstApps.SelectedItem = targetApp;
                    ToggleApp(targetApp);
                    e.Handled = true;
                    return;
                }
            }
        }

        // Space on selected item -> toggle
        if (e.Key == Key.Space && !isSearchFocused && LstApps.SelectedItem is AppInfo currentApp)
        {
            ToggleApp(currentApp);
            e.Handled = true;
            return;
        }
    }

    private void ToggleApp(AppInfo app)
    {
        if (app.IsRunning)
        {
            StopApp(app);
        }
        else if (app.IsLaunchable)
        {
            StartApp(app);
        }
    }

    private void LstApps_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateStatus();
    }

    private void LstApps_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (LstApps.SelectedItem is AppInfo app)
        {
            if (app.IsRunning)
            {
                OpenLogWindow(app);
            }
            else if (app.IsLaunchable)
            {
                StartApp(app);
            }
        }
    }

    private void BtnItemStart_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.FrameworkElement elem && elem.Tag is AppInfo app)
        {
            if (!app.IsRunning && app.IsLaunchable)
            {
                StartApp(app);
            }
        }
    }

    private void BtnItemStop_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.FrameworkElement elem && elem.Tag is AppInfo app)
        {
            if (app.IsRunning)
            {
                StopApp(app);
            }
        }
    }

    private void BtnItemLogs_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.FrameworkElement elem && elem.Tag is AppInfo app)
        {
            OpenLogWindow(app);
        }
    }

    private void CtxMenuStart_Click(object sender, RoutedEventArgs e)
    {
        if (LstApps.SelectedItem is AppInfo app && !app.IsRunning)
        {
            StartApp(app);
        }
    }

    private void CtxMenuStop_Click(object sender, RoutedEventArgs e)
    {
        if (LstApps.SelectedItem is AppInfo app && app.IsRunning)
        {
            StopApp(app);
        }
    }

    private void CtxMenuLogs_Click(object sender, RoutedEventArgs e)
    {
        if (LstApps.SelectedItem is AppInfo app)
        {
            OpenLogWindow(app);
        }
    }

    private void CtxMenuOpenFolder_Click(object sender, RoutedEventArgs e)
    {
        if (LstApps.SelectedItem is AppInfo app && !string.IsNullOrEmpty(app.LaunchTarget))
        {
            string target = app.LaunchTarget;
            if (app.IsUrl)
            {
                Process.Start(new ProcessStartInfo { FileName = target, UseShellExecute = true });
                return;
            }

            string? dir = Directory.Exists(target) ? target : Path.GetDirectoryName(target);
            if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = dir,
                    UseShellExecute = true
                });
            }
        }
    }

    private void CtxMenuCopyPath_Click(object sender, RoutedEventArgs e)
    {
        if (LstApps.SelectedItem is AppInfo app && !string.IsNullOrEmpty(app.LaunchTarget))
        {
            System.Windows.Clipboard.SetText(app.LaunchTarget);
        }
    }

    private void CtxMenuEdit_Click(object sender, RoutedEventArgs e)
    {
        OpenAppManagement();
    }

    private void BtnRefresh_Click(object sender, RoutedEventArgs e)
    {
        LoadApps();
    }

    private void BtnManage_Click(object sender, RoutedEventArgs e)
    {
        OpenAppManagement();
    }

    private void OpenAppManagement()
    {
        var manageWindow = new AppManagementWindow(_apps, _configuredHotkey, _configFilePath);
        manageWindow.Owner = this;
        manageWindow.ShowDialog();

        if (!string.IsNullOrWhiteSpace(manageWindow.ConfiguredHotkey) && manageWindow.ConfiguredHotkey != _configuredHotkey)
        {
            _configuredHotkey = manageWindow.ConfiguredHotkey;
            SetupHotkey();
        }

        if (!string.IsNullOrWhiteSpace(manageWindow.ConfigFilePath) && manageWindow.ConfigFilePath != _configFilePath)
        {
            _configFilePath = manageWindow.ConfigFilePath;
        }

        LoadApps(_configFilePath);
        RefreshShortcutsAndFilter();
    }

    private void BtnStartAll_Click(object sender, RoutedEventArgs e)
    {
        foreach (var app in _apps.Where(a => a.IsLaunchable && !a.IsRunning))
        {
            StartApp(app);
        }
    }

    private void BtnStopAll_Click(object sender, RoutedEventArgs e)
    {
        foreach (var app in _apps.Where(a => a.IsRunning))
        {
            StopApp(app);
        }
    }

    private void OpenLogWindow(AppInfo app)
    {
        if (_logWindows.TryGetValue(app.Name, out var existingWindow))
        {
            if (existingWindow.IsLoaded)
            {
                existingWindow.Activate();
                return;
            }
            _logWindows.Remove(app.Name);
        }

        var logWindow = new LogViewerWindow(app);
        logWindow.Owner = this;
        logWindow.Closing += (s, ev) => _logWindows.Remove(app.Name);
        _logWindows[app.Name] = logWindow;
        logWindow.Show();
    }

    private void StartApp(AppInfo app)
    {
        if (!app.IsLaunchable)
        {
            app.AddLog($"ERROR: No launch target defined for {app.Name}");
            return;
        }

        Task.Run(() =>
        {
            try
            {
                string rootDir = ConfigService.GetRootDir(_configFilePath);

                if (app.IsUrl)
                {
                    app.AddLog($"Opening URL: {app.LaunchTarget}");
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = app.LaunchTarget,
                        UseShellExecute = true
                    });
                    return;
                }

                var process = new Process();
                var startInfo = new ProcessStartInfo();

                if (app.IsDotnetProject)
                {
                    startInfo.FileName = "dotnet";
                    startInfo.Arguments = $"run --project \"{app.LaunchTarget}\"";
                    if (!string.IsNullOrWhiteSpace(app.Arguments))
                    {
                        startInfo.Arguments += $" -- {app.Arguments}";
                    }
                    startInfo.UseShellExecute = false;
                    startInfo.RedirectStandardOutput = true;
                    startInfo.RedirectStandardError = true;
                    startInfo.CreateNoWindow = true;

                    if (!string.IsNullOrEmpty(app.WorkingDirectory))
                    {
                        string workDir = Path.IsPathRooted(app.WorkingDirectory) ? app.WorkingDirectory : Path.GetFullPath(Path.Combine(rootDir, app.WorkingDirectory));
                        if (Directory.Exists(workDir)) startInfo.WorkingDirectory = workDir;
                    }
                }
                else
                {
                    string target = app.LaunchTarget;
                    string fullTarget = Path.IsPathRooted(target) ? target : Path.GetFullPath(Path.Combine(rootDir, target));
                    
                    startInfo.FileName = File.Exists(fullTarget) ? fullTarget : target;
                    
                    if (!string.IsNullOrWhiteSpace(app.Arguments))
                    {
                        startInfo.Arguments = app.Arguments;
                    }

                    if (!string.IsNullOrEmpty(app.WorkingDirectory))
                    {
                        string workDir = Path.IsPathRooted(app.WorkingDirectory) ? app.WorkingDirectory : Path.GetFullPath(Path.Combine(rootDir, app.WorkingDirectory));
                        if (Directory.Exists(workDir)) startInfo.WorkingDirectory = workDir;
                    }
                    else if (File.Exists(fullTarget))
                    {
                        startInfo.WorkingDirectory = Path.GetDirectoryName(fullTarget) ?? "";
                    }

                    if (app.RunAsAdmin)
                    {
                        startInfo.Verb = "runas";
                        startInfo.UseShellExecute = true;
                    }
                    else
                    {
                        // Check if it's a script / CLI or GUI app
                        bool isScriptOrCli = target.EndsWith(".bat", StringComparison.OrdinalIgnoreCase) ||
                                             target.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase) ||
                                             target.EndsWith(".ps1", StringComparison.OrdinalIgnoreCase);

                        if (isScriptOrCli)
                        {
                            startInfo.UseShellExecute = false;
                            startInfo.RedirectStandardOutput = true;
                            startInfo.RedirectStandardError = true;
                            startInfo.CreateNoWindow = true;
                        }
                        else
                        {
                            startInfo.UseShellExecute = true;
                        }
                    }
                }

                if (!startInfo.UseShellExecute)
                {
                    process.OutputDataReceived += (s, e) => { if (e.Data != null) app.AddLog(e.Data); };
                    process.ErrorDataReceived += (s, e) => { if (e.Data != null) app.AddLog(e.Data); };
                }

                process.StartInfo = startInfo;
                process.EnableRaisingEvents = true;
                process.Exited += (s, e) =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        app.Process = null;
                        UpdateStatus();
                    });
                };

                process.Start();
                app.Process = process;

                if (!startInfo.UseShellExecute)
                {
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();
                }

                Dispatcher.Invoke(() => UpdateStatus());
                app.AddLog($"Started {app.Name} (PID: {process.Id})");

                process.WaitForExit();
            }
            catch (Exception ex)
            {
                app.AddLog($"ERROR starting {app.Name}: {ex.Message}");
            }
            finally
            {
                app.Process = null;
                Dispatcher.Invoke(() => UpdateStatus());
            }
        });
    }

    private void UpdateStatus()
    {
        int runningCount = _apps.Count(a => a.IsRunning);
        int totalAppsCount = _apps.Count(a => a.IsLaunchable);
        LblStatus.Text = $"{runningCount} running • {totalAppsCount} total";
        StatusDot.Fill = runningCount > 0 
            ? new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#4ADE80"))
            : new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#94A3B8"));

        BtnStartAll.IsEnabled = _apps.Any(a => a.IsLaunchable && !a.IsRunning);
        BtnStopAll.IsEnabled = runningCount > 0;
    }

    private void StopApp(AppInfo app)
    {
        try
        {
            if (app.Process != null && !app.Process.HasExited)
            {
                app.Process.Kill(entireProcessTree: true);
                app.AddLog($"Stopped {app.Name}");
            }
        }
        catch (Exception ex)
        {
            app.AddLog($"ERROR stopping {app.Name}: {ex.Message}");
        }
    }
}
