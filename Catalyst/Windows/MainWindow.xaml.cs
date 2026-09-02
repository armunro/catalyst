using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using Catalyst.Adapters.UI.Navigation;
using Catalyst.Adapters.UI.ViewModels;
using Catalyst.Core.Ports.Inbound;
using Microsoft.Extensions.DependencyInjection;
using Wpf.Ui.Controls;

namespace Catalyst.Windows;

public partial class MainWindow : FluentWindow
{
    private const int SW_RESTORE = 9;

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    private readonly MainWindowViewModel _viewModel;
    private readonly IHotkeyService _hotkeyService;
    private readonly IWindowService _windowService;
    private readonly IAppLauncherService _launcherService;

    private System.Windows.Forms.NotifyIcon? _notifyIcon;
    private bool _isExiting = false;
    private System.Windows.Point _dragStartPoint;
    private bool _isDragging = false;

    public MainWindowViewModel ViewModel => _viewModel;

    public MainWindow() : this(
        CreateDefaultViewModel(),
        App.Services != null ? App.Services.GetRequiredService<IHotkeyService>() : new Core.Services.HotkeyService(new Adapters.Platform.WindowsHotkeyHook()),
        App.Services != null ? App.Services.GetRequiredService<IWindowService>() : new WindowService(App.Services!, new Adapters.Platform.WindowPlacementService()),
        App.Services != null ? App.Services.GetRequiredService<IAppLauncherService>() : new Core.Services.AppLauncherService(new Adapters.Processes.WindowsProcessExecutor(), new Core.Services.AppConfigurationService(new Adapters.Persistence.YamlConfigRepository(), new Adapters.Persistence.JsonSettingsStorage(), new Adapters.Persistence.PathResolver(new Adapters.Persistence.JsonSettingsStorage()))))
    {
    }

    private static MainWindowViewModel CreateDefaultViewModel()
    {
        if (App.Services != null)
        {
            return App.Services.GetRequiredService<MainWindowViewModel>();
        }

        var settings = new Adapters.Persistence.JsonSettingsStorage();
        var resolver = new Adapters.Persistence.PathResolver(settings);
        var repo = new Adapters.Persistence.YamlConfigRepository();
        var configService = new Core.Services.AppConfigurationService(repo, settings, resolver);
        var processExec = new Adapters.Processes.WindowsProcessExecutor();
        var launcherService = new Core.Services.AppLauncherService(processExec, configService);
        var hotkeyHook = new Adapters.Platform.WindowsHotkeyHook();
        var hotkeyService = new Core.Services.HotkeyService(hotkeyHook);
        var windowPlacement = new Adapters.Platform.WindowPlacementService();
        var windowService = new WindowService(App.Services!, windowPlacement);
        var renderer = new Adapters.Icons.SkiaIconRenderer();
        var iconService = new Core.Services.IconManagementService(renderer, configService);

        return new MainWindowViewModel(configService, launcherService, hotkeyService, windowService, iconService);
    }

    public MainWindow(
        MainWindowViewModel viewModel,
        IHotkeyService hotkeyService,
        IWindowService windowService,
        IAppLauncherService launcherService)
    {
        InitializeComponent();

        _viewModel = viewModel;
        _hotkeyService = hotkeyService;
        _windowService = windowService;
        _launcherService = launcherService;

        DataContext = _viewModel;
        LstApps.ItemsSource = _viewModel.AppsView;

        Loaded += MainWindow_Loaded;
        Closing += MainWindow_Closing;
        Closed += MainWindow_Closed;
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        _windowService.PositionBottomRight(this);
        SetupNotifyIcon();
        SetupHotkey();
        _viewModel.LoadApps();
        TxtSearch.Focus();
    }

    private void SetupNotifyIcon()
    {
        _notifyIcon = new System.Windows.Forms.NotifyIcon();
        try
        {
            var iconStream = System.Windows.Application.GetResourceStream(new Uri("pack://application:,,,/favicon.ico"))?.Stream;
            if (iconStream != null)
            {
                _notifyIcon.Icon = new System.Drawing.Icon(iconStream);
            }
            else
            {
                _notifyIcon.Icon = GetAppIconFallback();
            }
        }
        catch
        {
            _notifyIcon.Icon = GetAppIconFallback();
        }

        _notifyIcon.Text = "Catalyst - Dev App Launcher";
        _notifyIcon.Visible = true;

        var contextMenu = new System.Windows.Forms.ContextMenuStrip();
        contextMenu.Items.Add("Show Catalyst", null, (s, e) => Dispatcher.Invoke(RestoreWindow));
        contextMenu.Items.Add("Manage Apps", null, (s, e) => Dispatcher.Invoke(() => _viewModel.OpenAppManagement(this)));
        contextMenu.Items.Add("Settings", null, (s, e) => Dispatcher.Invoke(OpenSettings));
        contextMenu.Items.Add("-");
        contextMenu.Items.Add("Start All Visible", null, (s, e) => Dispatcher.Invoke(async () => await _viewModel.StartAllAsync()));
        contextMenu.Items.Add("Stop All", null, (s, e) => Dispatcher.Invoke(async () => await _viewModel.StopAllAsync()));
        contextMenu.Items.Add("-");
        contextMenu.Items.Add("Detach & Exit Catalyst", null, (s, e) => Dispatcher.Invoke(DetachAndExit));
        contextMenu.Items.Add("Exit (Stop All Apps)", null, (s, e) => Dispatcher.Invoke(ExitApplication));

        _notifyIcon.ContextMenuStrip = contextMenu;
        _notifyIcon.DoubleClick += (s, e) => Dispatcher.Invoke(RestoreWindow);
    }

    private static System.Drawing.Icon GetAppIconFallback()
    {
        try
        {
            string localIco = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "favicon.ico");
            if (System.IO.File.Exists(localIco))
            {
                return new System.Drawing.Icon(localIco);
            }

            var exePath = Environment.ProcessPath ?? System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
            if (!string.IsNullOrEmpty(exePath) && System.IO.File.Exists(exePath))
            {
                var extracted = System.Drawing.Icon.ExtractAssociatedIcon(exePath);
                if (extracted != null)
                {
                    return extracted;
                }
            }
        }
        catch
        {
            // Ignore and fall back
        }

        return System.Drawing.SystemIcons.Application;
    }

    private void SetupHotkey()
    {
        _hotkeyService.RegisterHotkey(this, _viewModel.ConfiguredHotkey, () =>
        {
            if (this.IsVisible && this.WindowState != WindowState.Minimized && this.IsActive)
            {
                this.Hide();
                this.ShowInTaskbar = false;
            }
            else
            {
                RestoreWindow();
            }
        });
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
        _hotkeyService.UnregisterHotkey();
        _notifyIcon?.Dispose();
    }

    public void RestoreWindow()
    {
        this.Show();
        this.ShowInTaskbar = true;
        this.WindowState = WindowState.Normal;
        _windowService.PositionBottomRight(this);

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
        Task.Run(async () => await _viewModel.StopAllAsync()).GetAwaiter().GetResult();
        _notifyIcon?.Dispose();
        _hotkeyService.UnregisterHotkey();
        System.Windows.Application.Current.Shutdown();
    }

    private void DetachAndExit()
    {
        _isExiting = true;
        _notifyIcon?.Dispose();
        _hotkeyService.UnregisterHotkey();
        System.Windows.Application.Current.Shutdown();
    }

    public void LoadApps(string? customPath = null)
    {
        _viewModel.LoadApps(customPath);
    }

    private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
    {
        _viewModel.SearchText = TxtSearch.Text;
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
            var target = LstApps.SelectedItem as AppInfo ?? (_viewModel.AppsView.Cast<AppInfo>().FirstOrDefault());
            if (target != null)
            {
                _ = _viewModel.ToggleAppAsync(target);
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

        if (e.Key == Key.F && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
        {
            TxtSearch.Focus();
            TxtSearch.SelectAll();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.M && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
        {
            _viewModel.OpenAppManagement(this);
            e.Handled = true;
            return;
        }

        if (e.Key == Key.OemComma && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
        {
            OpenSettings();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.F5)
        {
            _viewModel.LoadApps();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.L && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
        {
            if (LstApps.SelectedItem is AppInfo selectedApp && selectedApp.CanViewLogs)
            {
                _viewModel.OpenLogViewer(selectedApp, this);
                e.Handled = true;
                return;
            }
        }

        if (LstApps.SelectedItem is AppInfo appToMove)
        {
            bool isAlt = (Keyboard.Modifiers & ModifierKeys.Alt) == ModifierKeys.Alt;
            bool isCtrl = (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control;

            if ((isAlt || isCtrl) && e.Key == Key.Up)
            {
                _viewModel.MoveAppUp(appToMove);
                LstApps.SelectedItem = appToMove;
                LstApps.ScrollIntoView(appToMove);
                e.Handled = true;
                return;
            }
            if ((isAlt || isCtrl) && e.Key == Key.Down)
            {
                _viewModel.MoveAppDown(appToMove);
                LstApps.SelectedItem = appToMove;
                LstApps.ScrollIntoView(appToMove);
                e.Handled = true;
                return;
            }
        }

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
                var targetApp = _viewModel.AppsView.Cast<AppInfo>().FirstOrDefault(a => a.ShortcutIndex == digit.ToString());
                if (targetApp != null)
                {
                    LstApps.SelectedItem = targetApp;
                    _ = _viewModel.ToggleAppAsync(targetApp);
                    e.Handled = true;
                    return;
                }
            }
        }

        if (e.Key == Key.Space && !isSearchFocused && LstApps.SelectedItem is AppInfo currentApp)
        {
            _ = _viewModel.ToggleAppAsync(currentApp);
            e.Handled = true;
            return;
        }
    }

    private void LstApps_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
    }

    private void LstApps_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (LstApps.SelectedItem is AppInfo app)
        {
            if (app.IsRunning && app.CanViewLogs)
            {
                _viewModel.OpenLogViewer(app, this);
            }
            else if (app.IsLaunchable)
            {
                _ = _viewModel.StartAppAsync(app);
            }
        }
    }

    private void BtnItemStart_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.FrameworkElement elem && elem.Tag is AppInfo app)
        {
            if (!app.IsRunning && app.IsLaunchable)
            {
                _ = _viewModel.StartAppAsync(app);
            }
        }
    }

    private void BtnItemStop_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.FrameworkElement elem && elem.Tag is AppInfo app)
        {
            if (app.IsRunning)
            {
                _ = _viewModel.StopAppAsync(app);
            }
        }
    }

    private void BtnItemLogs_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.FrameworkElement elem && elem.Tag is AppInfo app && app.CanViewLogs)
        {
            _viewModel.OpenLogViewer(app, this);
        }
    }

    private void CtxMenuStart_Click(object sender, RoutedEventArgs e)
    {
        if (LstApps.SelectedItem is AppInfo app && !app.IsRunning)
        {
            _ = _viewModel.StartAppAsync(app);
        }
    }

    private void CtxMenuStop_Click(object sender, RoutedEventArgs e)
    {
        if (LstApps.SelectedItem is AppInfo app && app.IsRunning)
        {
            _ = _viewModel.StopAppAsync(app);
        }
    }

    private void CtxMenuLogs_Click(object sender, RoutedEventArgs e)
    {
        if (LstApps.SelectedItem is AppInfo app && app.CanViewLogs)
        {
            _viewModel.OpenLogViewer(app, this);
        }
    }

    private void CtxMenuMoveUp_Click(object sender, RoutedEventArgs e)
    {
        if (LstApps.SelectedItem is AppInfo app)
        {
            _viewModel.MoveAppUp(app);
            LstApps.SelectedItem = app;
            LstApps.ScrollIntoView(app);
        }
    }

    private void CtxMenuMoveDown_Click(object sender, RoutedEventArgs e)
    {
        if (LstApps.SelectedItem is AppInfo app)
        {
            _viewModel.MoveAppDown(app);
            LstApps.SelectedItem = app;
            LstApps.ScrollIntoView(app);
        }
    }

    public void MoveAppUp(AppInfo app)
    {
        _viewModel.MoveAppUp(app);
    }

    public void MoveAppDown(AppInfo app)
    {
        _viewModel.MoveAppDown(app);
    }

    private void LstApps_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dragStartPoint = e.GetPosition(null);
    }

    private void LstApps_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (e.LeftButton == System.Windows.Input.MouseButtonState.Pressed && !_isDragging)
        {
            System.Windows.Point position = e.GetPosition(null);
            if (Math.Abs(position.X - _dragStartPoint.X) > SystemParameters.MinimumHorizontalDragDistance ||
                Math.Abs(position.Y - _dragStartPoint.Y) > SystemParameters.MinimumVerticalDragDistance)
            {
                if (LstApps.SelectedItem is AppInfo selectedApp)
                {
                    _isDragging = true;
                    try
                    {
                        System.Windows.DragDrop.DoDragDrop(LstApps, selectedApp, System.Windows.DragDropEffects.Move);
                    }
                    finally
                    {
                        _isDragging = false;
                    }
                }
            }
        }
    }

    private void LstApps_DragOver(object sender, System.Windows.DragEventArgs e)
    {
        if (e.Data.GetDataPresent(typeof(AppInfo)))
        {
            e.Effects = System.Windows.DragDropEffects.Move;
            e.Handled = true;
        }
        else
        {
            e.Effects = System.Windows.DragDropEffects.None;
        }
    }

    private void LstApps_Drop(object sender, System.Windows.DragEventArgs e)
    {
        if (e.Data.GetData(typeof(AppInfo)) is AppInfo sourceApp)
        {
            int oldIndex = _viewModel.Apps.IndexOf(sourceApp);
            if (oldIndex < 0) return;

            var targetApp = GetAppInfoUnderMouse(e.GetPosition(LstApps));
            int newIndex;
            if (targetApp != null)
            {
                newIndex = _viewModel.Apps.IndexOf(targetApp);
                if (newIndex < 0) newIndex = _viewModel.Apps.Count - 1;
            }
            else
            {
                newIndex = _viewModel.Apps.Count - 1;
            }

            if (oldIndex != newIndex)
            {
                _viewModel.MoveApp(oldIndex, newIndex);
                LstApps.SelectedItem = sourceApp;
                LstApps.ScrollIntoView(sourceApp);
            }
        }
    }

    private AppInfo? GetAppInfoUnderMouse(System.Windows.Point position)
    {
        HitTestResult hitResult = VisualTreeHelper.HitTest(LstApps, position);
        if (hitResult?.VisualHit != null)
        {
            DependencyObject? current = hitResult.VisualHit;
            while (current != null && current != LstApps)
            {
                if (current is ListBoxItem lbi && lbi.DataContext is AppInfo app)
                {
                    return app;
                }
                current = VisualTreeHelper.GetParent(current);
            }
        }
        return null;
    }

    private void CtxMenuOpenFolder_Click(object sender, RoutedEventArgs e)
    {
        if (LstApps.SelectedItem is AppInfo app)
        {
            _viewModel.OpenFolder(app);
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
        _viewModel.OpenAppManagement(this);
    }

    private void BtnRefresh_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.LoadApps();
    }

    private void BtnManage_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.OpenAppManagement(this);
    }

    private void BtnSettings_Click(object sender, RoutedEventArgs e)
    {
        OpenSettings();
    }

    public void OpenSettings()
    {
        _viewModel.OpenSettings(this);
        ReRegisterHotkey();
    }

    public void ReRegisterHotkey()
    {
        _hotkeyService.UnregisterHotkey();
        SetupHotkey();
        LblHotkeyHint.Text = $" • Hotkey: {_viewModel.ConfiguredHotkey}";
    }

    private void BtnStartAll_Click(object sender, RoutedEventArgs e)
    {
        _ = _viewModel.StartAllAsync();
    }

    private void BtnStopAll_Click(object sender, RoutedEventArgs e)
    {
        _ = _viewModel.StopAllAsync();
    }
}
