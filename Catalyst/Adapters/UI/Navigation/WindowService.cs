using System;
using System.Collections.Generic;
using System.Windows;
using Catalyst.Core.Ports.Outbound;
using Catalyst.Windows;
using Microsoft.Extensions.DependencyInjection;

namespace Catalyst.Adapters.UI.Navigation;

public class WindowService : IWindowService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IWindowPlacementService _windowPlacementService;
    private readonly Dictionary<string, LogViewerWindow> _logWindows = new();

    public WindowService(
        IServiceProvider serviceProvider,
        IWindowPlacementService windowPlacementService)
    {
        _serviceProvider = serviceProvider;
        _windowPlacementService = windowPlacementService;
    }

    public void ShowMainWindow()
    {
        var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
        mainWindow.Show();
        mainWindow.Activate();
    }

    public void ShowAppManagement(Window? owner = null)
    {
        var window = _serviceProvider != null 
            ? _serviceProvider.GetRequiredService<AppManagementWindow>()
            : new AppManagementWindow();
        if (owner != null)
        {
            window.Owner = owner;
        }
        window.ShowDialog();
    }

    public void ShowSettings(Window? owner = null)
    {
        var window = _serviceProvider != null
            ? _serviceProvider.GetRequiredService<SettingsWindow>()
            : new SettingsWindow();
        if (owner != null)
        {
            window.Owner = owner;
        }
        window.ShowDialog();
    }

    public void ShowLogViewer(AppInfo app, Window? owner = null)
    {
        if (app == null || !app.CanViewLogs) return;

        if (_logWindows.TryGetValue(app.Name, out var existingWindow))
        {
            if (existingWindow.IsLoaded)
            {
                existingWindow.WindowState = WindowState.Normal;
                existingWindow.Activate();
                return;
            }
            _logWindows.Remove(app.Name);
        }

        var logWindow = new LogViewerWindow(app);
        if (owner != null)
        {
            logWindow.Owner = owner;
        }

        logWindow.Closing += (_, _) => _logWindows.Remove(app.Name);
        _logWindows[app.Name] = logWindow;
        logWindow.Show();
    }

    public void ShowIconsResult(string iconsPath, List<(string AppName, string IconPath)> generatedIcons, Window? owner = null)
    {
        var window = new IconsResultWindow(iconsPath, generatedIcons);
        if (owner != null)
        {
            window.Owner = owner;
        }
        window.ShowDialog();
    }

    public void PositionBottomRight(Window window)
    {
        _windowPlacementService.PositionBottomRight(window);
    }
}
