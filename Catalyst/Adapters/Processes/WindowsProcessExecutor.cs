using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Catalyst.Core.Ports.Outbound;

namespace Catalyst.Adapters.Processes;

public class WindowsProcessExecutor : IProcessExecutor
{
    public Task<Process?> StartProcessAsync(
        AppInfo app,
        string rootDir,
        Action<string> onOutputReceived,
        Action<Process> onExited)
    {
        if (app.IsUrl)
        {
            OpenUrl(app.LaunchTarget);
            return Task.FromResult<Process?>(null);
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
                string workDir = Path.IsPathRooted(app.WorkingDirectory)
                    ? app.WorkingDirectory
                    : Path.GetFullPath(Path.Combine(rootDir, app.WorkingDirectory));
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
                string workDir = Path.IsPathRooted(app.WorkingDirectory)
                    ? app.WorkingDirectory
                    : Path.GetFullPath(Path.Combine(rootDir, app.WorkingDirectory));
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
            process.OutputDataReceived += (_, e) =>
            {
                if (e.Data != null)
                {
                    onOutputReceived(e.Data);
                }
            };

            process.ErrorDataReceived += (_, e) =>
            {
                if (e.Data != null)
                {
                    onOutputReceived(e.Data);
                }
            };
        }

        process.StartInfo = startInfo;
        process.EnableRaisingEvents = true;
        process.Exited += (_, _) =>
        {
            onExited(process);
        };

        try
        {
            process.Start();

            if (!startInfo.UseShellExecute)
            {
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
            }

            return Task.FromResult<Process?>(process);
        }
        catch (Exception ex)
        {
            onOutputReceived($"ERROR starting {app.Name}: {ex.Message}");
            return Task.FromResult<Process?>(null);
        }
    }

    public async Task<bool> StopProcessAsync(Process process, Action<string>? logCallback = null)
    {
        try
        {
            if (process.HasExited)
                return true;

            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch (Exception ex)
            {
                logCallback?.Invoke($"[Error terminating process tree] {ex.Message}");
            }

            using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(5));
            try
            {
                await process.WaitForExitAsync(cts.Token);
                return true;
            }
            catch (OperationCanceledException)
            {
                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = "taskkill",
                        Arguments = $"/F /T /PID {process.Id}",
                        CreateNoWindow = true,
                        UseShellExecute = false
                    };
                    Process.Start(psi)?.WaitForExit(3000);
                }
                catch (Exception ex)
                {
                    logCallback?.Invoke($"[Error force killing PID {process.Id}] {ex.Message}");
                }
                return process.HasExited;
            }
        }
        catch (Exception ex)
        {
            logCallback?.Invoke($"[Error stopping process] {ex.Message}");
            return false;
        }
    }

    public bool IsProcessRunning(Process? process)
    {
        if (process == null) return false;
        try
        {
            return !process.HasExited;
        }
        catch
        {
            return false;
        }
    }

    public void OpenUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to open URL {url}: {ex.Message}");
        }
    }

    public void OpenDirectory(string directoryPath)
    {
        try
        {
            if (Directory.Exists(directoryPath))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"\"{directoryPath}\"",
                    UseShellExecute = true
                });
            }
            else if (File.Exists(directoryPath))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"/select,\"{directoryPath}\"",
                    UseShellExecute = true
                });
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to open directory {directoryPath}: {ex.Message}");
        }
    }
}
