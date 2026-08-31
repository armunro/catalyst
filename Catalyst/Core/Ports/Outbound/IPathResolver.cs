namespace Catalyst.Core.Ports.Outbound;

public interface IPathResolver
{
    string? CustomConfigPath { get; set; }
    string? ParseCommandLineConfigPath(string[]? args = null);
    string? GetEnvironmentConfigPath();
    string GetDefaultConfigPath();
    string GetActiveConfigPath(string[]? args = null);
    string GetRootDir(string? configPath = null);
}
