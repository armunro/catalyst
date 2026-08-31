namespace Catalyst.Core.Ports.Outbound;

public interface IConfigRepository
{
    AppConfigFile? LoadConfigFile(string configPath);
    void SaveConfigFile(AppConfigFile config, string configPath);
}
