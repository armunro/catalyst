using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Catalyst.Core.Ports.Inbound;

public interface IAppLauncherService
{
    Task LaunchAppAsync(AppInfo app, Action<string>? onLog = null);
    Task StopAppAsync(AppInfo app, Action<string>? onLog = null);
    Task StartAllAsync(IEnumerable<AppInfo> apps, Action<string>? onLog = null);
    Task StopAllAsync(IEnumerable<AppInfo> apps, Action<string>? onLog = null);
    void DetectRunningApps(IEnumerable<AppInfo> apps);
    void OpenAppLocation(AppInfo app);
}
