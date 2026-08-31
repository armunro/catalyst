using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Catalyst.Core.Ports.Outbound;

public interface IProcessExecutor
{
    Task<Process?> StartProcessAsync(
        AppInfo app,
        string rootDir,
        Action<string> onOutputReceived,
        Action<Process> onExited);

    Task<bool> StopProcessAsync(Process process, Action<string>? logCallback = null);

    bool IsProcessRunning(Process? process);

    void OpenUrl(string url);

    void OpenDirectory(string directoryPath);
}
