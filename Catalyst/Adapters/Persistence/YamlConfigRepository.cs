using System.IO;
using Catalyst.Core.Ports.Outbound;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Catalyst.Adapters.Persistence;

public class YamlConfigRepository : IConfigRepository
{
    public AppConfigFile? LoadConfigFile(string configPath)
    {
        if (!File.Exists(configPath))
            return null;

        string yaml = File.ReadAllText(configPath);

        // First try CamelCase naming convention
        try
        {
            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .IgnoreUnmatchedProperties()
                .Build();

            var result = deserializer.Deserialize<AppConfigFile>(yaml);
            if (result != null && (result.Apps.Count > 0 || !string.IsNullOrWhiteSpace(result.Hotkey)))
            {
                return result;
            }
        }
        catch { }

        // Fallback to Underscored naming convention for legacy YAML files
        try
        {
            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(UnderscoredNamingConvention.Instance)
                .IgnoreUnmatchedProperties()
                .Build();

            return deserializer.Deserialize<AppConfigFile>(yaml);
        }
        catch
        {
            return null;
        }
    }

    public void SaveConfigFile(AppConfigFile config, string configPath)
    {
        string? dir = Path.GetDirectoryName(configPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        var serializer = new SerializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();

        string yaml = serializer.Serialize(config);
        File.WriteAllText(configPath, yaml);
    }
}
