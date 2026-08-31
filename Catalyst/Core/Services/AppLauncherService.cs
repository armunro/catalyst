using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Catalyst.Core.Ports.Inbound;
using Catalyst.Core.Ports.Outbound;

namespace Catalyst.Core.Services;

public class AppLauncherService : IAppLauncherService
{
    private readonly IProcessExecutor _processExecutor;
    private readonly IAppConfigurationService _configurationService;

    public AppLauncherService(
        IProcessExecutor processExecutor,
        IAppConfigurationService configurationService)
    {
        _processExecutor = processExecutor;
        _configurationService = configurationService;
    }

    public async Task LaunchAppAsync(AppInfo app, Action<string>? onLog = null)
    {
        if (!app.IsLaunchable)
        {
            string err = $"ERROR: No launch target defined for {app.Name}";
            app.AddLog(err);
            onLog?.Invoke(err);
            return;
        }

        string rootDir = _configurationService.RootDir;

        if (app.IsUrl)
        {
            string msg = $"Opening URL: {app.LaunchTarget}";
            app.AddLog(msg);
            onLog?.Invoke(msg);
            _processExecutor.OpenUrl(app.LaunchTarget);
            return;
        }

        var process = await _processExecutor.StartProcessAsync(
            app,
            rootDir,
            output =>
            {
                app.AddLog(output);
                onLog?.Invoke(output);
            },
            exitedProcess =>
            {
                app.Process = null;
            });

        if (process != null)
        {
            app.Process = process;
            string msg = $"Started {app.Name} (PID: {process.Id})";
            app.AddLog(msg);
            onLog?.Invoke(msg);
        }
    }

    public async Task StopAppAsync(AppInfo app, Action<string>? onLog = null)
    {
        if (app.Process != null)
        {
            string msg = $"Stopping {app.Name}...";
            app.AddLog(msg);
            onLog?.Invoke(msg);

            bool stopped = await _processExecutor.StopProcessAsync(app.Process, log =>
            {
                app.AddLog(log);
                onLog?.Invoke(log);
            });

            if (stopped)
            {
                app.Process = null;
                string stoppedMsg = $"Stopped {app.Name}";
                app.AddLog(stoppedMsg);
                onLog?.Invoke(stoppedMsg);
            }
        }
    }

    public async Task StartAllAsync(IEnumerable<AppInfo> apps, Action<string>? onLog = null)
    {
        var launchable = apps.Where(a => !a.IsHidden && a.IsLaunchable && !a.IsRunning).ToList();
        foreach (var app in launchable)
        {
            await LaunchAppAsync(app, onLog);
        }
    }

    public async Task StopAllAsync(IEnumerable<AppInfo> apps, Action<string>? onLog = null)
    {
        var running = apps.Where(a => !a.IsHidden && a.IsRunning).ToList();
        foreach (var app in running)
        {
            await StopAppAsync(app, onLog);
        }
    }

    public void DetectRunningApps(IEnumerable<AppInfo> apps)
    {
        foreach (var app in apps)
        {
            if (!app.IsLaunchable) continue;

            string target = app.LaunchTarget;
            string processName = Path.GetFileNameWithoutExtension(target);
            if (string.IsNullOrWhiteSpace(processName)) continue;

            try
            {
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
                            process.Exited += (_, _) =>
                            {
                                app.Process = null;
                            };

                            if (process.HasExited)
                            {
                                app.Process = null;
                            }
                        }
                        catch { }
                    });
                }
            }
            catch { }
        }
    }

    public void OpenAppLocation(AppInfo app)
    {
        if (string.IsNullOrEmpty(app.LaunchTarget)) return;

        if (app.IsUrl)
        {
            _processExecutor.OpenUrl(app.LaunchTarget);
            return;
        }

        string rootDir = _configurationService.RootDir;
        string target = app.LaunchTarget;
        string fullTarget = Path.IsPathRooted(target) ? target : Path.GetFullPath(Path.Combine(rootDir, target));

        string? dir = Directory.Exists(fullTarget) ? fullTarget : Path.GetDirectoryName(fullTarget);
        if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
        {
            _processExecutor.OpenDirectory(dir);
        }
    }
}
