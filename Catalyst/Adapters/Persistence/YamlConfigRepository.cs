using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Catalyst.Core.Ports.Outbound;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Catalyst.Adapters.Persistence;

public class YamlConfigRepository : IConfigRepository
{
    public AppConfigFile? LoadConfigFile(string configPath)
    {
        if (string.IsNullOrWhiteSpace(configPath) || !File.Exists(configPath))
            return null;

        string yaml = string.Empty;
        for (int i = 0; i < 3; i++)
        {
            try
            {
                using var stream = new FileStream(configPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var reader = new StreamReader(stream);
                yaml = reader.ReadToEnd();
                break;
            }
            catch (IOException) when (i < 2)
            {
                Thread.Sleep(30);
            }
            catch
            {
                return null;
            }
        }

        if (string.IsNullOrWhiteSpace(yaml))
            return new AppConfigFile();

        var conventions = new INamingConvention[]
        {
            CamelCaseNamingConvention.Instance,
            UnderscoredNamingConvention.Instance,
            PascalCaseNamingConvention.Instance,
            HyphenatedNamingConvention.Instance,
            NullNamingConvention.Instance
        };

        AppConfigFile? bestResult = null;
        int maxScore = -1;

        foreach (var convention in conventions)
        {
            try
            {
                var deserializer = new DeserializerBuilder()
                    .WithNamingConvention(convention)
                    .IgnoreUnmatchedProperties()
                    .Build();

                var result = deserializer.Deserialize<AppConfigFile>(yaml);
                if (result != null)
                {
                    int score = ScoreConfig(result);
                    if (score > maxScore)
                    {
                        maxScore = score;
                        bestResult = result;
                    }
                }
            }
            catch { }
        }

        return bestResult ?? new AppConfigFile();
    }

    private static int ScoreConfig(AppConfigFile config)
    {
        int score = 0;
        if (!string.IsNullOrWhiteSpace(config.Hotkey))
        {
            score += 5;
        }

        if (config.Apps != null)
        {
            score += config.Apps.Count * 10;
            foreach (var app in config.Apps)
            {
                if (!string.IsNullOrWhiteSpace(app.Name)) score += 5;
                if (!string.IsNullOrWhiteSpace(app.CatalystDirectory)) score += 2;
                if (!string.IsNullOrWhiteSpace(app.Launch?.ProjectPath)) score += 5;
                if (!string.IsNullOrWhiteSpace(app.Launch?.ExecutablePath)) score += 5;
                if (!string.IsNullOrWhiteSpace(app.Launch?.Arguments)) score += 2;
                if (!string.IsNullOrWhiteSpace(app.Launch?.WorkingDirectory)) score += 2;
                if (!string.IsNullOrWhiteSpace(app.Icon?.Color)) score += 2;
                if (!string.IsNullOrWhiteSpace(app.Icon?.SecondaryColor)) score += 2;
                if (!string.IsNullOrWhiteSpace(app.Icon?.BackgroundType)) score += 2;
                if (!string.IsNullOrWhiteSpace(app.Icon?.GradientDirection)) score += 2;
                if (!string.IsNullOrWhiteSpace(app.Icon?.Label)) score += 2;
                if (!string.IsNullOrWhiteSpace(app.Icon?.IconPath)) score += 2;
                if (!string.IsNullOrWhiteSpace(app.Icon?.FaviconPath)) score += 2;
                if (!string.IsNullOrWhiteSpace(app.Icon?.BootstrapIcon)) score += 2;
                if (!string.IsNullOrWhiteSpace(app.Icon?.CustomGlyphSvg)) score += 2;
                if (!string.IsNullOrWhiteSpace(app.Icon?.CustomGlyphColor)) score += 2;
                if (!string.IsNullOrWhiteSpace(app.Icon?.SvgOverride)) score += 2;
            }
        }

        return score;
    }

    public void SaveConfigFile(AppConfigFile config, string configPath)
    {
        if (string.IsNullOrWhiteSpace(configPath)) return;

        for (int i = 0; i < 3; i++)
        {
            try
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

                using var stream = new FileStream(configPath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite);
                using var writer = new StreamWriter(stream);
                writer.Write(yaml);
                writer.Flush();
                break;
            }
            catch (IOException) when (i < 2)
            {
                Thread.Sleep(30);
            }
            catch
            {
                break;
            }
        }
    }
}
