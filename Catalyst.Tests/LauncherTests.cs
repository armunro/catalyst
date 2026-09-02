using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Media;
using Catalyst;
using Catalyst.Services;
using Microsoft.Extensions.DependencyInjection;
using SkiaSharp;
using Xunit;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Catalyst.Tests;

public class LauncherTests
{
    [Fact]
    public void AppInfo_DetectsGeneralApplicationAndDotnetCorrectly()
    {
        var appDotnet = new AppInfo
        {
            Name = "MyDotnetApp",
            ProjectPath = @"C:\Source\MyApp\MyApp.csproj"
        };
        Assert.True(appDotnet.IsDotnetProject);
        Assert.False(appDotnet.IsUrl);
        Assert.True(appDotnet.IsLaunchable);
        Assert.Equal("MyApp.csproj", appDotnet.ShortLaunchTarget);
        Assert.Equal(".NET Project", appDotnet.LaunchTypeDescription);

        var appExe = new AppInfo
        {
            Name = "VSCode",
            ExecutablePath = @"C:\Program Files\VSCode\Code.exe"
        };
        Assert.False(appExe.IsDotnetProject);
        Assert.False(appExe.IsUrl);
        Assert.True(appExe.IsLaunchable);
        Assert.Equal("Code.exe", appExe.ShortLaunchTarget);
        Assert.Equal("Executable", appExe.LaunchTypeDescription);

        var appUrl = new AppInfo
        {
            Name = "Docs",
            ExecutablePath = "https://github.com/microsoft/terminal"
        };
        Assert.False(appUrl.IsDotnetProject);
        Assert.True(appUrl.IsUrl);
        Assert.True(appUrl.IsLaunchable);
        Assert.False(appUrl.CanMonitorProcess);
        Assert.False(appUrl.CanViewLogs);
        Assert.False(appUrl.IsRunning);
        Assert.Equal("Web URL", appUrl.LaunchTypeDescription);

        var appScript = new AppInfo
        {
            Name = "DeployScript",
            ExecutablePath = @"C:\Scripts\deploy.bat"
        };
        Assert.Equal("Batch Script", appScript.LaunchTypeDescription);
    }

    [Theory]
    [InlineData("Alt+Space", true)]
    [InlineData("Ctrl+Shift+Space", true)]
    [InlineData("Ctrl+Alt+C", true)]
    [InlineData("Win+Alt+C", true)]
    [InlineData("Alt+OemTilde", true)]
    [InlineData("InvalidKeyComboXYZ", false)]
    [InlineData("", false)]
    public void HotkeyManager_ParsesHotkeysCorrectly(string hotkey, bool expectedValid)
    {
        bool success = HotkeyManager.TryParseHotkey(hotkey, out uint mod, out uint vk);
        Assert.Equal(expectedValid, success);
        if (expectedValid)
        {
            Assert.True(vk > 0);
        }
    }

    [Fact]
    public void IconGenerator_RendersCompleteSvgOverride()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "CatalystTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            string svgContent = @"<svg xmlns=""http://www.w3.org/2000/svg"" viewBox=""0 0 100 100"">
                <circle cx=""50"" cy=""50"" r=""40"" fill=""#ff5500""/>
                <polygon points=""30,70 70,70 50,30"" fill=""#ffffff""/>
            </svg>";

            var app = new AppInfo
            {
                Name = "SvgApp",
                SvgOverride = svgContent
            };

            var generator = new IconGenerator();
            using var bitmap = generator.RenderIconBitmap(app, 512, tempDir);

            Assert.NotNull(bitmap);
            Assert.Equal(512, bitmap.Width);
            Assert.Equal(512, bitmap.Height);

            // Generate files to ensure ICO and PNGs are created without error
            generator.GenerateIcon(app, tempDir);

            string mainPng = Path.Combine(tempDir, "SvgApp", "SvgApp.png");
            string ico = Path.Combine(tempDir, "SvgApp", "favicon.ico");
            string png32 = Path.Combine(tempDir, "SvgApp", "favicon-32x32.png");

            Assert.True(File.Exists(mainPng));
            Assert.True(File.Exists(ico));
            Assert.True(File.Exists(png32));
            Assert.True(new FileInfo(mainPng).Length > 0);
            Assert.True(new FileInfo(ico).Length > 0);

            // Also check that all multi-resolution favicons are generated and non-empty
            string png16 = Path.Combine(tempDir, "SvgApp", "favicon-16x16.png");
            string png48 = Path.Combine(tempDir, "SvgApp", "favicon-48x48.png");
            Assert.True(File.Exists(png16));
            Assert.True(File.Exists(png48));
            Assert.True(new FileInfo(png16).Length > 0);
            Assert.True(new FileInfo(png48).Length > 0);
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    [Fact]
    public void IconGenerator_RendersCustomGlyphSvg()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "CatalystTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            string glyphSvg = @"<svg xmlns=""http://www.w3.org/2000/svg"" viewBox=""0 0 24 24"">
                <path d=""M12 2L2 7l10 5 10-5-10-5zM2 17l10 5 10-5M2 12l10 5 10-5""/>
            </svg>";

            var app = new AppInfo
            {
                Name = "CustomGlyphApp",
                Color = "#8957e5",
                CustomGlyphSvg = glyphSvg,
                CustomGlyphColor = "#FFFF00"
            };

            var generator = new IconGenerator();
            using var bitmap = generator.RenderIconBitmap(app, 512, tempDir);

            Assert.NotNull(bitmap);
            Assert.Equal(512, bitmap.Width);
            Assert.Equal(512, bitmap.Height);

            generator.GenerateIcon(app, tempDir);

            string mainPng = Path.Combine(tempDir, "CustomGlyphApp", "CustomGlyphApp.png");
            string ico = Path.Combine(tempDir, "CustomGlyphApp", "favicon.ico");
            string svgPath = Path.Combine(tempDir, "CustomGlyphApp", "CustomGlyphApp.svg");

            Assert.True(File.Exists(mainPng));
            Assert.True(File.Exists(ico));
            Assert.True(File.Exists(svgPath));

            string svgContent = File.ReadAllText(svgPath);
            Assert.Contains("<svg", svgContent);
            Assert.Contains("</svg>", svgContent);
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    [Fact]
    public void IconGenerator_RendersRawPathDataCustomGlyph()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "CatalystTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            string rawPath = "M10 10 H 90 V 90 H 10 Z";

            var app = new AppInfo
            {
                Name = "RawPathApp",
                Color = "#0373fc",
                CustomGlyphSvg = rawPath
            };

            var generator = new IconGenerator();
            using var bitmap = generator.RenderIconBitmap(app, 512, tempDir);

            Assert.NotNull(bitmap);
            Assert.Equal(512, bitmap.Width);
            Assert.Equal(512, bitmap.Height);
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    [Fact]
    public void AppsYaml_SerializesAndDeserializesAllFieldsBackwardsCompatibly()
    {
        string yamlInput = @"hotkey: Alt+Space
apps:
- name: TestGeneralApp
  launch:
    executablePath: C:\Tools\tool.exe
    arguments: --flag 123
    workingDirectory: C:\Tools
    runAsAdmin: true
  icon:
    color: '#00AAFF'
    label: TG
    customGlyphSvg: icons\tool-glyph.svg
    customGlyphColor: '#FFFFFF'
    svgOverride: icons\full.svg
";

        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();

        var config = deserializer.Deserialize<AppConfigFile>(yamlInput);

        Assert.NotNull(config);
        Assert.Equal("Alt+Space", config.Hotkey);
        Assert.Single(config.Apps);

        var entry = config.Apps[0];
        Assert.Equal("TestGeneralApp", entry.Name);
        Assert.Equal(@"C:\Tools\tool.exe", entry.Launch.ExecutablePath);
        Assert.Equal("--flag 123", entry.Launch.Arguments);
        Assert.Equal(@"C:\Tools", entry.Launch.WorkingDirectory);
        Assert.True(entry.Launch.RunAsAdmin);

        Assert.Equal("#00AAFF", entry.Icon.Color);
        Assert.Equal("TG", entry.Icon.Label);
        Assert.Equal(@"icons\tool-glyph.svg", entry.Icon.CustomGlyphSvg);
        Assert.Equal("#FFFFFF", entry.Icon.CustomGlyphColor);
        Assert.Equal(@"icons\full.svg", entry.Icon.SvgOverride);

        // Serialize back and verify
        var serializer = new SerializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();
        string reserialized = serializer.Serialize(config);
        Assert.Contains("TestGeneralApp", reserialized);
        Assert.Contains("executablePath", reserialized);
        Assert.Contains("customGlyphSvg", reserialized);
        Assert.Contains("svgOverride", reserialized);
    }

    [Fact]
    public void AppsYaml_DeserializesLegacyFormatWithoutError()
    {
        string legacyYaml = @"apps:
- name: FlightPlan
  launch:
    projectPath: FlightPlan\FlightPlan.csproj
  icon:
    color: '#ff5e00'
    label: FP
    iconPath: icons\FlightPlan\FlightPlan.png
    faviconPath: FlightPlan\wwwroot\favicon.ico
    bootstrapIcon: send
";

        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();

        var config = deserializer.Deserialize<AppConfigFile>(legacyYaml);

        Assert.NotNull(config);
        Assert.Single(config.Apps);
        Assert.Equal("FlightPlan", config.Apps[0].Name);
        Assert.Equal(@"FlightPlan\FlightPlan.csproj", config.Apps[0].Launch.ProjectPath);
        Assert.Equal("send", config.Apps[0].Icon.BootstrapIcon);
    }

    [Fact]
    public void VisibilityConverters_HandleBitmapSourceAndNullCorrectly()
    {
        var notNullConverter = new NotNullToVisibilityConverter();
        var nullConverter = new NullToVisibilityConverter();
        var stringConverter = new StringNullOrEmptyToVisibilityConverter();

        var dummyBitmap = System.Windows.Media.Imaging.BitmapSource.Create(
            1, 1, 96, 96, System.Windows.Media.PixelFormats.Bgra32, null, new byte[4], 4);

        // NotNullToVis should be Visible for BitmapSource, Collapsed for null
        Assert.Equal(System.Windows.Visibility.Visible, notNullConverter.Convert(dummyBitmap, typeof(System.Windows.Visibility), null!, System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal(System.Windows.Visibility.Collapsed, notNullConverter.Convert(null!, typeof(System.Windows.Visibility), null!, System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal(System.Windows.Visibility.Collapsed, notNullConverter.Convert("", typeof(System.Windows.Visibility), null!, System.Globalization.CultureInfo.InvariantCulture));

        // NullToVis should be Collapsed for BitmapSource, Visible for null
        Assert.Equal(System.Windows.Visibility.Collapsed, nullConverter.Convert(dummyBitmap, typeof(System.Windows.Visibility), null!, System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal(System.Windows.Visibility.Visible, nullConverter.Convert(null!, typeof(System.Windows.Visibility), null!, System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal(System.Windows.Visibility.Visible, nullConverter.Convert("", typeof(System.Windows.Visibility), null!, System.Globalization.CultureInfo.InvariantCulture));

        // StringNullOrEmptyToVis should also handle non-string objects gracefully
        Assert.Equal(System.Windows.Visibility.Visible, stringConverter.Convert(dummyBitmap, typeof(System.Windows.Visibility), null!, System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal(System.Windows.Visibility.Collapsed, stringConverter.Convert(null!, typeof(System.Windows.Visibility), null!, System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public void IconGenerator_SvgOverride_AlwaysPreservesFullVectorColors()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "CatalystTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            // SVG with a dark background (#0F0623) and purple element (#B03BFF)
            string multiColorSvg = @"<svg xmlns=""http://www.w3.org/2000/svg"" viewBox=""0 0 100 100"">
                <rect width=""100"" height=""100"" fill=""#0f0623""/>
                <circle cx=""50"" cy=""50"" r=""25"" fill=""#b03bff""/>
            </svg>";

            var appSvgOverride = new AppInfo
            {
                Name = "SvgOverrideApp",
                SvgOverride = multiColorSvg,
                CustomGlyphColor = "#FFFFFF" // Even if CustomGlyphColor has a value, SvgOverride should NOT be tinted
            };

            var generator = new IconGenerator();
            using var bitmap = generator.RenderIconBitmap(appSvgOverride, 100, tempDir);
            Assert.NotNull(bitmap);

            // Center pixel (circle) should be purple (#B03BFF), NOT white
            var centerPixel = bitmap.GetPixel(50, 50);
            Assert.True(centerPixel.Red > 150, $"Expected Red > 150 but was {centerPixel.Red}");
            Assert.True(centerPixel.Green < 80, $"Expected Green < 80 but was {centerPixel.Green}");
            Assert.True(centerPixel.Blue > 200, $"Expected Blue > 200 but was {centerPixel.Blue}");

            // Background pixel should be dark purple/black (#0F0623), NOT white
            var cornerPixel = bitmap.GetPixel(10, 10);
            Assert.True(cornerPixel.Red < 30, $"Expected Red < 30 but was {cornerPixel.Red}");
            Assert.True(cornerPixel.Green < 30, $"Expected Green < 30 but was {cornerPixel.Green}");
            Assert.True(cornerPixel.Blue < 60, $"Expected Blue < 60 but was {cornerPixel.Blue}");
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    [Fact]
    public void IconGenerator_CustomGlyphSvg_SupportsColorPreservationAndTinting()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "CatalystTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            string redGlyphSvg = @"<svg xmlns=""http://www.w3.org/2000/svg"" viewBox=""0 0 100 100"">
                <circle cx=""50"" cy=""50"" r=""40"" fill=""#FF0000""/>
            </svg>";

            // 1. Untinted Custom Glyph SVG (CustomGlyphColor is empty) -> preserves original red
            var appUntinted = new AppInfo
            {
                Name = "UntintedGlyphApp",
                Color = "#1E88E4",
                CustomGlyphSvg = redGlyphSvg,
                CustomGlyphColor = ""
            };

            var generator = new IconGenerator();
            using var bitmapUntinted = generator.RenderIconBitmap(appUntinted, 100, tempDir);
            Assert.NotNull(bitmapUntinted);

            var centerUntinted = bitmapUntinted.GetPixel(50, 50);
            Assert.Equal(255, centerUntinted.Red);
            Assert.Equal(0, centerUntinted.Green);
            Assert.Equal(0, centerUntinted.Blue);

            // 2. Tinted Custom Glyph SVG (CustomGlyphColor is green #00FF00) -> turns glyph green
            var appTinted = new AppInfo
            {
                Name = "TintedGlyphApp",
                Color = "#1E88E4",
                CustomGlyphSvg = redGlyphSvg,
                CustomGlyphColor = "#00FF00"
            };

            using var bitmapTinted = generator.RenderIconBitmap(appTinted, 100, tempDir);
            Assert.NotNull(bitmapTinted);

            var centerTinted = bitmapTinted.GetPixel(50, 50);
            Assert.Equal(0, centerTinted.Red);
            Assert.Equal(255, centerTinted.Green);
            Assert.Equal(0, centerTinted.Blue);
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    [Fact]
    public void IconGenerator_RendersSolidBackgroundCorrectly()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "CatalystTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var app = new AppInfo
            {
                Name = "SolidApp",
                Color = "#FF0000", // Pure red
                BackgroundType = "Solid",
                Label = ""
            };

            var generator = new IconGenerator();
            using var bitmap = generator.RenderIconBitmap(app, 100, tempDir);
            Assert.NotNull(bitmap);

            // In solid mode, center pixel and offset pixels should match exact solid color #FF0000
            var centerPixel = bitmap.GetPixel(50, 50);
            Assert.Equal(255, centerPixel.Red);
            Assert.Equal(0, centerPixel.Green);
            Assert.Equal(0, centerPixel.Blue);

            var innerPixel = bitmap.GetPixel(30, 30);
            Assert.Equal(255, innerPixel.Red);
            Assert.Equal(0, innerPixel.Green);
            Assert.Equal(0, innerPixel.Blue);
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    [Fact]
    public void IconGenerator_RendersTwoPartGradientCorrectly()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "CatalystTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var app = new AppInfo
            {
                Name = "GradientApp",
                Color = "#FF0000",           // Red start
                SecondaryColor = "#0000FF",  // Blue end
                BackgroundType = "Gradient",
                GradientDirection = "Vertical",
                Label = ""
            };

            var generator = new IconGenerator();
            using var bitmap = generator.RenderIconBitmap(app, 100, tempDir);
            Assert.NotNull(bitmap);

            // Vertical gradient: top should be predominantly red, bottom predominantly blue
            var topPixel = bitmap.GetPixel(50, 15);
            Assert.True(topPixel.Red > 200, $"Expected top pixel to be red but was {topPixel.Red}");
            Assert.True(topPixel.Blue < 60, $"Expected top pixel to have low blue but was {topPixel.Blue}");

            var bottomPixel = bitmap.GetPixel(50, 85);
            Assert.True(bottomPixel.Blue > 200, $"Expected bottom pixel to be blue but was {bottomPixel.Blue}");
            Assert.True(bottomPixel.Red < 60, $"Expected bottom pixel to have low red but was {bottomPixel.Red}");
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    [Fact]
    public void AppInfo_ColorBrush_ReflectsSolidAndGradientModes()
    {
        var solidApp = new AppInfo
        {
            Color = "#FF0000",
            BackgroundType = "Solid"
        };
        var solidBrush = solidApp.ColorBrush;
        Assert.IsType<System.Windows.Media.SolidColorBrush>(solidBrush);

        var gradApp = new AppInfo
        {
            Color = "#FF0000",
            SecondaryColor = "#0000FF",
            BackgroundType = "Gradient",
            GradientDirection = "Horizontal"
        };
        var gradBrush = gradApp.ColorBrush;
        Assert.IsType<System.Windows.Media.LinearGradientBrush>(gradBrush);
        var linearGrad = (System.Windows.Media.LinearGradientBrush)gradBrush;
        Assert.Equal(0.0, linearGrad.StartPoint.X);
        Assert.Equal(0.5, linearGrad.StartPoint.Y);
        Assert.Equal(1.0, linearGrad.EndPoint.X);
        Assert.Equal(0.5, linearGrad.EndPoint.Y);
    }

    [Fact]
    public void AppsYaml_SerializesAndDeserializesBackgroundProperties()
    {
        var original = new AppConfigFile
        {
            Hotkey = "Alt+Space",
            Apps = new List<AppConfigEntry>
            {
                new()
                {
                    Name = "GradientTestApp",
                    Icon = new IconConfig
                    {
                        Color = "#8A2387",
                        SecondaryColor = "#E94057",
                        BackgroundType = "Gradient",
                        GradientDirection = "DiagonalUp"
                    }
                }
            }
        };

        var serializer = new SerializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();
        string yaml = serializer.Serialize(original);

        Assert.Contains("secondaryColor: '#E94057'", yaml);
        Assert.Contains("backgroundType: Gradient", yaml);
        Assert.Contains("gradientDirection: DiagonalUp", yaml);

        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();
        var deserialized = deserializer.Deserialize<AppConfigFile>(yaml);

        Assert.NotNull(deserialized);
        Assert.Single(deserialized.Apps);
        Assert.Equal("#8A2387", deserialized.Apps[0].Icon.Color);
        Assert.Equal("#E94057", deserialized.Apps[0].Icon.SecondaryColor);
        Assert.Equal("Gradient", deserialized.Apps[0].Icon.BackgroundType);
        Assert.Equal("DiagonalUp", deserialized.Apps[0].Icon.GradientDirection);
    }

    [Fact]
    public void IconGenerator_CosmicSvg_RendersWithoutWhiteout()
    {
        string cosmicPath = @"C:\Users\AndrewM\OneDrive - Munro\Projects\Cosmic\_Brand\COSMIC.svg";
        if (File.Exists(cosmicPath))
        {
            var app = new AppInfo
            {
                Name = "CosmicApp",
                SvgOverride = cosmicPath,
                CustomGlyphColor = "#FFFFFF" // Even if residual glyph color exists, SvgOverride preserves dark/purple colors
            };

            var generator = new IconGenerator();
            using var bitmap = generator.RenderIconBitmap(app, 512, ".");
            Assert.NotNull(bitmap);

            // Check that the rendered icon is not solid white
            // The top-left background should be dark (#0F0623)
            var bgPixel = bitmap.GetPixel(64, 128);
            Assert.True(bgPixel.Red < 50, $"Expected dark background red < 50 but was {bgPixel.Red}");
            Assert.True(bgPixel.Green < 50, $"Expected dark background green < 50 but was {bgPixel.Green}");
            Assert.True(bgPixel.Blue < 60, $"Expected dark background blue < 60 but was {bgPixel.Blue}");
        }
    }

    [Theory]
    [InlineData(new string[] { "app.exe", "--config", @"C:\Custom\apps.yaml" }, @"C:\Custom\apps.yaml")]
    [InlineData(new string[] { "app.exe", "-c", @"D:\Configs\apps.yaml" }, @"D:\Configs\apps.yaml")]
    [InlineData(new string[] { "app.exe", "/config", @"E:\MyApps.yaml" }, @"E:\MyApps.yaml")]
    [InlineData(new string[] { "app.exe", @"--config=F:\apps.yaml" }, @"F:\apps.yaml")]
    [InlineData(new string[] { "app.exe", @"/config:G:\custom.yml" }, @"G:\custom.yml")]
    [InlineData(new string[] { "app.exe", @"-c:H:\custom.yaml" }, @"H:\custom.yaml")]
    [InlineData(new string[] { "app.exe", @"I:\Direct\apps.yaml" }, @"I:\Direct\apps.yaml")]
    [InlineData(new string[] { "app.exe", @"I:\Direct\apps.yml" }, @"I:\Direct\apps.yml")]
    [InlineData(new string[] { "app.exe" }, null)]
    [InlineData(new string[] { "app.exe", "--other-flag" }, null)]
    public void ConfigService_ParseCommandLineConfigPath_ParsesCorrectly(string[] args, string? expected)
    {
        string? result = ConfigService.ParseCommandLineConfigPath(args);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ConfigService_UserConfigPath_PersistsAndLoadsCorrectly()
    {
        string tempSettingsDir = Path.Combine(Path.GetTempPath(), "CatalystSettings_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempSettingsDir);
        string originalSettingsPath = ConfigService.SettingsFilePath;

        try
        {
            string settingsFile = Path.Combine(tempSettingsDir, "settings.json");
            ConfigService.SettingsFilePath = settingsFile;

            Assert.Null(ConfigService.GetUserConfigPath());

            string customPath = @"C:\CustomLocation\my-apps.yaml";
            ConfigService.SetUserConfigPath(customPath);

            Assert.True(File.Exists(settingsFile));
            Assert.Equal(customPath, ConfigService.GetUserConfigPath());

            ConfigService.SetUserConfigPath(null);
            Assert.Null(ConfigService.GetUserConfigPath());
        }
        finally
        {
            ConfigService.SettingsFilePath = originalSettingsPath;
            try { Directory.Delete(tempSettingsDir, true); } catch { }
        }
    }

    [Fact]
    public void ConfigService_GetRootDir_ReturnsContainingDirectory()
    {
        string configPath = @"C:\Users\Developer\Projects\Catalyst\apps.yaml";
        string rootDir = ConfigService.GetRootDir(configPath);
        Assert.Equal(@"C:\Users\Developer\Projects\Catalyst", rootDir);
    }

    [Fact]
    public void ConfigService_GetActiveConfigPath_HonorsPriorityOrder()
    {
        string tempSettingsDir = Path.Combine(Path.GetTempPath(), "CatalystPriority_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempSettingsDir);
        string originalSettingsPath = ConfigService.SettingsFilePath;
        string? originalCustom = ConfigService.CustomConfigPath;

        try
        {
            string settingsFile = Path.Combine(tempSettingsDir, "settings.json");
            ConfigService.SettingsFilePath = settingsFile;
            ConfigService.CustomConfigPath = null;

            // 1. Default fallback
            string defaultPath = ConfigService.GetDefaultConfigPath();
            Assert.Equal(defaultPath, ConfigService.GetActiveConfigPath(Array.Empty<string>()));

            // 2. User setting overrides default
            string userPath = Path.Combine(tempSettingsDir, "user-apps.yaml");
            ConfigService.SetUserConfigPath(userPath);
            Assert.Equal(Path.GetFullPath(userPath), ConfigService.GetActiveConfigPath(Array.Empty<string>()));

            // 3. CLI args override user setting
            string cliPath = Path.Combine(tempSettingsDir, "cli-apps.yaml");
            string[] cliArgs = new string[] { "app.exe", "--config", cliPath };
            Assert.Equal(Path.GetFullPath(cliPath), ConfigService.GetActiveConfigPath(cliArgs));

            // 4. CustomConfigPath overrides everything
            string customPath = Path.Combine(tempSettingsDir, "custom-apps.yaml");
            ConfigService.CustomConfigPath = customPath;
            Assert.Equal(Path.GetFullPath(customPath), ConfigService.GetActiveConfigPath(cliArgs));
        }
        finally
        {
            ConfigService.SettingsFilePath = originalSettingsPath;
            ConfigService.CustomConfigPath = originalCustom;
            try { Directory.Delete(tempSettingsDir, true); } catch { }
        }
    }

    [Fact]
    public void ConfigService_LoadAndSaveConfigFile_RoundTripsAccurately()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "CatalystConfigTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        string targetYamlPath = Path.Combine(tempDir, "custom-folder", "my-apps.yaml");

        try
        {
            var config = new AppConfigFile
            {
                Hotkey = "Ctrl+Alt+K",
                Apps = new List<AppConfigEntry>
                {
                    new()
                    {
                        Name = "CustomLoadedApp",
                        Launch = new LaunchConfig
                        {
                            ProjectPath = @"SubProject\App.csproj",
                            Arguments = "--verbose",
                            RunAsAdmin = true
                        },
                        Icon = new IconConfig
                        {
                            Color = "#112233",
                            Label = "CLA"
                        }
                    }
                }
            };

            ConfigService.SaveConfigFile(config, targetYamlPath);
            Assert.True(File.Exists(targetYamlPath));

            var loaded = ConfigService.LoadConfigFile(targetYamlPath);
            Assert.NotNull(loaded);
            Assert.Equal("Ctrl+Alt+K", loaded.Hotkey);
            Assert.Single(loaded.Apps);
            Assert.Equal("CustomLoadedApp", loaded.Apps[0].Name);
            Assert.Equal(@"SubProject\App.csproj", loaded.Apps[0].Launch.ProjectPath);
            Assert.Equal("--verbose", loaded.Apps[0].Launch.Arguments);
            Assert.True(loaded.Apps[0].Launch.RunAsAdmin);
            Assert.Equal("#112233", loaded.Apps[0].Icon.Color);
            Assert.Equal("CLA", loaded.Apps[0].Icon.Label);
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    [Fact]
    public void AppsYaml_SerializesAndDeserializesHiddenApps()
    {
        string yamlInput = @"hotkey: Alt+Space
apps:
- name: VisibleApp
  hidden: false
  launch:
    executablePath: C:\Tools\tool.exe
- name: HiddenApp
  hidden: true
  launch:
    executablePath: C:\Tools\hidden.exe
";

        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();

        var config = deserializer.Deserialize<AppConfigFile>(yamlInput);

        Assert.NotNull(config);
        Assert.Equal(2, config.Apps.Count);
        Assert.False(config.Apps[0].Hidden);
        Assert.True(config.Apps[1].Hidden);

        var serializer = new SerializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();

        string serialized = serializer.Serialize(config);
        Assert.Contains("hidden: false", serialized);
        Assert.Contains("hidden: true", serialized);

        var roundTripped = deserializer.Deserialize<AppConfigFile>(serialized);
        Assert.NotNull(roundTripped);
        Assert.False(roundTripped.Apps[0].Hidden);
        Assert.True(roundTripped.Apps[1].Hidden);
    }

    [Fact]
    public void AppInfo_HiddenProperty_NotifiesPropertyChanged()
    {
        var app = new AppInfo
        {
            Name = "TestApp",
            IsHidden = false
        };

        var changedProperties = new List<string>();
        app.PropertyChanged += (sender, args) =>
        {
            if (args.PropertyName != null)
                changedProperties.Add(args.PropertyName);
        };

        app.IsHidden = true;
        Assert.True(app.IsHidden);
        Assert.True(app.Hidden);
        Assert.Contains(nameof(AppInfo.IsHidden), changedProperties);
        Assert.Contains(nameof(AppInfo.Hidden), changedProperties);

        changedProperties.Clear();
        app.Hidden = false;
        Assert.False(app.IsHidden);
        Assert.False(app.Hidden);
        Assert.Contains(nameof(AppInfo.IsHidden), changedProperties);
        Assert.Contains(nameof(AppInfo.Hidden), changedProperties);
    }

    [Fact]
    public void ConfigService_CreateConfigFile_MaintainsAppOrderAfterReorder()
    {
        var app1 = new AppInfo { Name = "App1", ExecutablePath = @"C:\App1.exe" };
        var app2 = new AppInfo { Name = "App2", ExecutablePath = @"C:\App2.exe" };
        var app3 = new AppInfo { Name = "App3", ExecutablePath = @"C:\App3.exe" };

        var appsList = new ObservableCollection<AppInfo> { app1, app2, app3 };

        // Initial order: App1, App2, App3
        var initialConfig = ConfigService.CreateConfigFile(appsList, "Alt+Space", @"C:\");
        Assert.Equal(3, initialConfig.Apps.Count);
        Assert.Equal("App1", initialConfig.Apps[0].Name);
        Assert.Equal("App2", initialConfig.Apps[1].Name);
        Assert.Equal("App3", initialConfig.Apps[2].Name);

        // Move App3 to index 0 (top)
        appsList.Move(2, 0);

        var reorderedConfig = ConfigService.CreateConfigFile(appsList, "Alt+Space", @"C:\");
        Assert.Equal(3, reorderedConfig.Apps.Count);
        Assert.Equal("App3", reorderedConfig.Apps[0].Name);
        Assert.Equal("App1", reorderedConfig.Apps[1].Name);
        Assert.Equal("App2", reorderedConfig.Apps[2].Name);

        // Move App1 down to index 2 (bottom)
        appsList.Move(1, 2);

        var reorderedConfig2 = ConfigService.CreateConfigFile(appsList, "Alt+Space", @"C:\");
        Assert.Equal(3, reorderedConfig2.Apps.Count);
        Assert.Equal("App3", reorderedConfig2.Apps[0].Name);
        Assert.Equal("App2", reorderedConfig2.Apps[1].Name);
        Assert.Equal("App1", reorderedConfig2.Apps[2].Name);
    }

    [Fact]
    public void ConfigService_SaveAndLoad_PreservesReorderedApps()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "CatalystReorderTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        string configPath = Path.Combine(tempDir, "apps.yaml");

        try
        {
            var apps = new ObservableCollection<AppInfo>
            {
                new() { Name = "FirstApp", ExecutablePath = @"C:\First.exe", IsHidden = false },
                new() { Name = "SecondApp", ExecutablePath = @"C:\Second.exe", IsHidden = true },
                new() { Name = "ThirdApp", ExecutablePath = @"C:\Third.exe", IsHidden = false }
            };

            // Reorder: swap FirstApp and ThirdApp
            apps.Move(2, 0);

            ConfigService.SaveConfigFile(apps, "Ctrl+Shift+A", configPath);
            Assert.True(File.Exists(configPath));

            var loadedConfig = ConfigService.LoadConfigFile(configPath);
            Assert.NotNull(loadedConfig);
            Assert.Equal("Ctrl+Shift+A", loadedConfig.Hotkey);
            Assert.Equal(3, loadedConfig.Apps.Count);
            Assert.Equal("ThirdApp", loadedConfig.Apps[0].Name);
            Assert.False(loadedConfig.Apps[0].Hidden);
            Assert.Equal("FirstApp", loadedConfig.Apps[1].Name);
            Assert.False(loadedConfig.Apps[1].Hidden);
            Assert.Equal("SecondApp", loadedConfig.Apps[2].Name);
            Assert.True(loadedConfig.Apps[2].Hidden);
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    [Fact]
    public void ListCollectionView_MainWindowFilter_DoesNotFilterAppManagementWindowView()
    {
        var apps = new ObservableCollection<AppInfo>
        {
            new() { Name = "VisibleApp", ExecutablePath = @"C:\Visible.exe", IsHidden = false },
            new() { Name = "HiddenApp", ExecutablePath = @"C:\Hidden.exe", IsHidden = true }
        };

        // MainWindow view with filter hiding hidden apps
        var mainWindowView = new System.Windows.Data.ListCollectionView(apps)
        {
            Filter = item => item is AppInfo a && !a.IsHidden
        };

        // AppManagement view without filter
        var appManagementView = new System.Windows.Data.ListCollectionView(apps);

        var mainWindowList = mainWindowView.Cast<AppInfo>().ToList();
        var appManagementList = appManagementView.Cast<AppInfo>().ToList();

        Assert.Single(mainWindowList);
        Assert.Equal("VisibleApp", mainWindowList[0].Name);

        Assert.Equal(2, appManagementList.Count);
        Assert.Contains(appManagementList, a => a.Name == "HiddenApp");
        Assert.Contains(appManagementList, a => a.Name == "VisibleApp");
    }

    [Fact]
    public void DependencyInjection_RegistersAllHexagonalPortsAndAdapters()
    {
        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
        Catalyst.DependencyInjection.ServiceCollectionExtensions.AddCatalystServices(services);
        var provider = services.BuildServiceProvider();

        // Check Outbound Driven Ports
        Assert.NotNull(provider.GetService<Catalyst.Core.Ports.Outbound.IConfigRepository>());
        Assert.NotNull(provider.GetService<Catalyst.Core.Ports.Outbound.ISettingsStorage>());
        Assert.NotNull(provider.GetService<Catalyst.Core.Ports.Outbound.IPathResolver>());
        Assert.NotNull(provider.GetService<Catalyst.Core.Ports.Outbound.IProcessExecutor>());
        Assert.NotNull(provider.GetService<Catalyst.Core.Ports.Outbound.IIconRenderer>());
        Assert.NotNull(provider.GetService<Catalyst.Core.Ports.Outbound.IGlobalHotkeyHook>());
        Assert.NotNull(provider.GetService<Catalyst.Core.Ports.Outbound.IWindowPlacementService>());

        // Check Inbound Driving Ports / Application Services
        Assert.NotNull(provider.GetService<Catalyst.Core.Ports.Inbound.IAppConfigurationService>());
        Assert.NotNull(provider.GetService<Catalyst.Core.Ports.Inbound.IAppLauncherService>());
        Assert.NotNull(provider.GetService<Catalyst.Core.Ports.Inbound.IIconManagementService>());
        Assert.NotNull(provider.GetService<Catalyst.Core.Ports.Inbound.IHotkeyService>());

        // Check UI & Navigation
        Assert.NotNull(provider.GetService<Catalyst.Adapters.UI.Navigation.IWindowService>());
        Assert.NotNull(provider.GetService<Catalyst.Adapters.UI.ViewModels.MainWindowViewModel>());
        Assert.NotNull(provider.GetService<Catalyst.Adapters.UI.ViewModels.AppManagementViewModel>());
    }

    [Fact]
    public void MainWindowViewModel_FilterAndReordering_WorksAsExpected()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "CatalystMainVMTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        string tempConfigFile = Path.Combine(tempDir, "apps.yaml");

        try
        {
            var mockConfigRepo = new Catalyst.Adapters.Persistence.YamlConfigRepository();
            var mockSettings = new Catalyst.Adapters.Persistence.JsonSettingsStorage();
            var mockPathResolver = new Catalyst.Adapters.Persistence.PathResolver(mockSettings);
            mockPathResolver.CustomConfigPath = tempConfigFile;
            var configService = new Catalyst.Core.Services.AppConfigurationService(mockConfigRepo, mockSettings, mockPathResolver);
            var processExecutor = new Catalyst.Adapters.Processes.WindowsProcessExecutor();
            var launcherService = new Catalyst.Core.Services.AppLauncherService(processExecutor, configService);
            var hotkeyService = new Catalyst.Core.Services.HotkeyService(new Catalyst.Adapters.Platform.WindowsHotkeyHook());
            var windowService = new Catalyst.Adapters.UI.Navigation.WindowService(null!, new Catalyst.Adapters.Platform.WindowPlacementService());

            var vm = new Catalyst.Adapters.UI.ViewModels.MainWindowViewModel(
                configService,
                launcherService,
                hotkeyService,
                windowService);

            var app1 = new AppInfo { Name = "AlphaApp", ExecutablePath = @"C:\Alpha.exe", IsHidden = false };
            var app2 = new AppInfo { Name = "BetaApp", ExecutablePath = @"C:\Beta.exe", IsHidden = true };
            var app3 = new AppInfo { Name = "GammaApp", ExecutablePath = @"C:\Gamma.exe", IsHidden = false };

            vm.Apps.Add(app1);
            vm.Apps.Add(app2);
            vm.Apps.Add(app3);

            vm.RefreshFilterAndShortcuts();

            // Hidden app should not be in visible AppsView
            var visible = vm.AppsView.Cast<AppInfo>().ToList();
            Assert.Equal(2, visible.Count);
            Assert.Equal("AlphaApp", visible[0].Name);
            Assert.Equal("GammaApp", visible[1].Name);
            Assert.Equal("1", visible[0].ShortcutIndex);
            Assert.Equal("2", visible[1].ShortcutIndex);

            // Filter by SearchText
            vm.SearchText = "Gamma";
            var filtered = vm.AppsView.Cast<AppInfo>().ToList();
            Assert.Single(filtered);
            Assert.Equal("GammaApp", filtered[0].Name);
            Assert.Equal("1", filtered[0].ShortcutIndex);

            // Reset search
            vm.SearchText = "";
            Assert.Equal(2, vm.AppsView.Cast<AppInfo>().Count());

            // Move Down
            vm.MoveAppDown(app1);
            var visibleAfterMove = vm.AppsView.Cast<AppInfo>().ToList();
            Assert.Equal("GammaApp", visibleAfterMove[0].Name);
            Assert.Equal("AlphaApp", visibleAfterMove[1].Name);
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    [Fact]
    public void AppManagementViewModel_CRUD_WorksAsExpected()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "CatalystVMTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        string tempConfigFile = Path.Combine(tempDir, "apps.yaml");

        File.WriteAllText(tempConfigFile, @"hotkey: Alt+Space
apps:
  - name: InitialApp
    path: C:\Initial.exe
");

        try
        {
            var mockConfigRepo = new Catalyst.Adapters.Persistence.YamlConfigRepository();
            var mockSettings = new Catalyst.Adapters.Persistence.JsonSettingsStorage();
            var mockPathResolver = new Catalyst.Adapters.Persistence.PathResolver(mockSettings);
            mockPathResolver.CustomConfigPath = tempConfigFile;
            var configService = new Catalyst.Core.Services.AppConfigurationService(mockConfigRepo, mockSettings, mockPathResolver);
            var iconRenderer = new Catalyst.Adapters.Icons.SkiaIconRenderer();
            var iconService = new Catalyst.Core.Services.IconManagementService(iconRenderer, configService);
            var hotkeyService = new Catalyst.Core.Services.HotkeyService(new Catalyst.Adapters.Platform.WindowsHotkeyHook());
            var windowService = new Catalyst.Adapters.UI.Navigation.WindowService(null!, new Catalyst.Adapters.Platform.WindowPlacementService());

            var vm = new Catalyst.Adapters.UI.ViewModels.AppManagementViewModel(
                configService,
                iconService,
                hotkeyService,
                windowService);

            // Verify constructor loads apps from configuration automatically
            Assert.Single(vm.Apps);
            Assert.Equal("InitialApp", vm.Apps[0].Name);

            var app1 = new AppInfo { Name = "AppA", ExecutablePath = @"C:\A.exe" };
            var app2 = new AppInfo { Name = "AppB", ExecutablePath = @"C:\B.exe" };

            vm.Initialize(new List<AppInfo> { app1, app2 }, "Alt+Space", tempConfigFile);

            Assert.Equal(2, vm.Apps.Count);
            Assert.Equal(app1, vm.SelectedApp);

            // Add New App
            var newApp = vm.AddNewApp();
            Assert.Equal(3, vm.Apps.Count);
            Assert.Equal(newApp, vm.SelectedApp);

            // Move Up
            vm.MoveAppUp(newApp);
            Assert.Equal(1, vm.Apps.IndexOf(newApp));

            // Delete App
            vm.DeleteApp(newApp);
            Assert.Equal(2, vm.Apps.Count);
            Assert.DoesNotContain(newApp, vm.Apps);

            // Validate Hotkey
            Assert.True(vm.ValidateHotkey("Ctrl+Shift+A"));
            Assert.False(vm.ValidateHotkey("InvalidKeyNameHere"));
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    private class FakeProcessExecutor : Catalyst.Core.Ports.Outbound.IProcessExecutor
    {
        public bool StartCalled { get; private set; }
        public bool StopCalled { get; private set; }
        public bool UrlOpened { get; private set; }
        public string? LastOpenedUrl { get; private set; }

        public System.Threading.Tasks.Task<System.Diagnostics.Process?> StartProcessAsync(
            AppInfo app,
            string rootDir,
            Action<string> onOutputReceived,
            Action<System.Diagnostics.Process> onExited)
        {
            StartCalled = true;
            onOutputReceived($"[TEST] Started {app.Name}");
            // Return null or simulated process
            return System.Threading.Tasks.Task.FromResult<System.Diagnostics.Process?>(null);
        }

        public System.Threading.Tasks.Task<bool> StopProcessAsync(System.Diagnostics.Process process, Action<string>? logCallback = null)
        {
            StopCalled = true;
            logCallback?.Invoke("[TEST] Stopped");
            return System.Threading.Tasks.Task.FromResult(true);
        }

        public bool IsProcessRunning(System.Diagnostics.Process? process) => false;

        public void OpenUrl(string url)
        {
            UrlOpened = true;
            LastOpenedUrl = url;
        }

        public void OpenDirectory(string directoryPath) { }
    }

    [Fact]
    public async System.Threading.Tasks.Task AppLauncherService_ExecutesViaProcessExecutorPort()
    {
        var fakeExecutor = new FakeProcessExecutor();
        var mockConfigRepo = new Catalyst.Adapters.Persistence.YamlConfigRepository();
        var mockSettings = new Catalyst.Adapters.Persistence.JsonSettingsStorage();
        var mockPathResolver = new Catalyst.Adapters.Persistence.PathResolver(mockSettings);
        mockPathResolver.CustomConfigPath = Path.Combine(Path.GetTempPath(), "catalyst_test_dummy.yaml");
        var configService = new Catalyst.Core.Services.AppConfigurationService(mockConfigRepo, mockSettings, mockPathResolver);

        var launcherService = new Catalyst.Core.Services.AppLauncherService(fakeExecutor, configService);

        var app = new AppInfo
        {
            Name = "TestCLIApp",
            ExecutablePath = @"C:\TestApp.exe"
        };

        var logs = new List<string>();
        await launcherService.LaunchAppAsync(app, msg => logs.Add(msg));

        Assert.True(fakeExecutor.StartCalled);
        Assert.Contains(logs, l => l.Contains("Started TestCLIApp"));

        // URL launch test
        var urlApp = new AppInfo
        {
            Name = "WebDashboard",
            ExecutablePath = "https://localhost:5001"
        };

        await launcherService.LaunchAppAsync(urlApp);
        Assert.True(fakeExecutor.UrlOpened);
        Assert.Equal("https://localhost:5001", fakeExecutor.LastOpenedUrl);
    }

    [Fact]
    public void SkiaIconRenderer_RenderIconSvg_OutputsValidSvgWithLabelAndGradient()
    {
        var renderer = new Catalyst.Adapters.Icons.SkiaIconRenderer();
        var app = new AppInfo
        {
            Name = "SvgTestApp",
            Color = "#FF0000",
            SecondaryColor = "#0000FF",
            BackgroundType = "Gradient",
            GradientDirection = "Vertical",
            Label = "SVG"
        };

        string svg = renderer.RenderIconSvg(app, 512, ".");
        Assert.NotNull(svg);
        Assert.Contains("<linearGradient", svg);
        Assert.Contains("viewBox=\"0 0 512 512\"", svg);

        // Load the generated SVG using Svg.Skia and render it to a bitmap to verify pixels
        var skSvg = new Svg.Skia.SKSvg();
        skSvg.FromSvg(svg);
        Assert.NotNull(skSvg.Picture);

        using var bitmap = new SkiaSharp.SKBitmap(512, 512);
        using (var canvas = new SkiaSharp.SKCanvas(bitmap))
        {
            canvas.Clear(SkiaSharp.SKColors.Transparent);
            canvas.DrawPicture(skSvg.Picture);
        }

        var topPixel = bitmap.GetPixel(256, 100);
        var bottomPixel = bitmap.GetPixel(256, 450);

        // Top should be reddish, bottom should be bluish
        Assert.True(topPixel.Red > 150 && topPixel.Blue < 100, $"Top pixel was {topPixel}");
        Assert.True(bottomPixel.Blue > 150 && bottomPixel.Red < 100, $"Bottom pixel was {bottomPixel}");
    }

    [Fact]
    public void SkiaIconRenderer_RenderIconSvg_OutputsValidSvgWithDefaultGradient()
    {
        var renderer = new Catalyst.Adapters.Icons.SkiaIconRenderer();
        var app = new AppInfo
        {
            Name = "SvgTestAppDefaultGrad",
            Color = "#1E88E4",
            SecondaryColor = null,
            BackgroundType = "Gradient",
            GradientDirection = "Vertical",
            Label = "SVG"
        };

        string svg = renderer.RenderIconSvg(app, 512, ".");
        Assert.NotNull(svg);
        Assert.Contains("<linearGradient", svg);
        Assert.Contains("viewBox=\"0 0 512 512\"", svg);

        var skSvg = new Svg.Skia.SKSvg();
        skSvg.FromSvg(svg);
        Assert.NotNull(skSvg.Picture);

        using var bitmap = new SkiaSharp.SKBitmap(512, 512);
        using (var canvas = new SkiaSharp.SKCanvas(bitmap))
        {
            canvas.Clear(SkiaSharp.SKColors.Transparent);
            canvas.DrawPicture(skSvg.Picture);
        }

        var topPixel = bitmap.GetPixel(256, 50);
        var bottomPixel = bitmap.GetPixel(256, 450);

        // Default gradient: top is lighter (higher RGB), bottom is darker (lower RGB)
        Assert.True(topPixel.Red > bottomPixel.Red, $"Expected top red {topPixel.Red} > bottom red {bottomPixel.Red}");
        Assert.True(topPixel.Green > bottomPixel.Green, $"Expected top green {topPixel.Green} > bottom green {bottomPixel.Green}");
        Assert.True(topPixel.Blue > bottomPixel.Blue, $"Expected top blue {topPixel.Blue} > bottom blue {bottomPixel.Blue}");
    }

    [Fact]
    public void SkiaIconRenderer_RenderIconSvg_ImplicitGradientWhenSecondaryColorPresent()
    {
        var renderer = new Catalyst.Adapters.Icons.SkiaIconRenderer();
        var app = new AppInfo
        {
            Name = "SvgTestAppImplicitGrad",
            Color = "#FF0000",
            SecondaryColor = "#00FF00",
            BackgroundType = "", // Implicit gradient
            GradientDirection = "Horizontal"
        };

        string svg = renderer.RenderIconSvg(app, 512, ".");
        Assert.NotNull(svg);
        Assert.Contains("<linearGradient", svg);
        Assert.Contains("viewBox=\"0 0 512 512\"", svg);
    }

    [Fact]
    public void IconManagementService_GenerateIcon_GeneratesSvgFile()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "CatalystSvgTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var mockConfigRepo = new Catalyst.Adapters.Persistence.YamlConfigRepository();
            var mockSettings = new Catalyst.Adapters.Persistence.JsonSettingsStorage(Path.Combine(tempDir, "settings.json"));
            var mockPathResolver = new Catalyst.Adapters.Persistence.PathResolver(mockSettings);
            mockPathResolver.CustomConfigPath = Path.Combine(tempDir, "apps.yaml");
            var configService = new Catalyst.Core.Services.AppConfigurationService(mockConfigRepo, mockSettings, mockPathResolver);

            var renderer = new Catalyst.Adapters.Icons.SkiaIconRenderer();
            var iconService = new Catalyst.Core.Services.IconManagementService(renderer, configService);

            var app = new AppInfo
            {
                Name = "ServiceSvgApp",
                Color = "#123456",
                Label = "SV"
            };

            iconService.GenerateIcon(app);

            string iconsDir = Path.Combine(tempDir, "icons");
            string svgPath = Path.Combine(iconsDir, "ServiceSvgApp", "ServiceSvgApp.svg");
            string pngPath = Path.Combine(iconsDir, "ServiceSvgApp", "ServiceSvgApp.png");
            string icoPath = Path.Combine(iconsDir, "ServiceSvgApp", "favicon.ico");

            Assert.True(File.Exists(svgPath));
            Assert.True(File.Exists(pngPath));
            Assert.True(File.Exists(icoPath));

            string svgContent = File.ReadAllText(svgPath);
            Assert.Contains("<svg", svgContent);
            Assert.Contains("</svg>", svgContent);
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    [Fact]
    public void IconGenerator_RenderIconSvg_And_SaveSvg_WorkCorrectly()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "CatalystSvgTest2_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var generator = new IconGenerator();
            var app = new AppInfo
            {
                Name = "DirectSvgApp",
                Color = "#112233",
                SecondaryColor = "#445566",
                BackgroundType = "Gradient",
                GradientDirection = "Horizontal",
                CustomGlyphSvg = @"<svg xmlns=""http://www.w3.org/2000/svg"" viewBox=""0 0 10 10""><circle cx=""5"" cy=""5"" r=""4"" fill=""white"" /></svg>",
                CustomGlyphColor = "#FFFFFF"
            };

            string svg = generator.RenderIconSvg(app, 256, tempDir);
            Assert.NotNull(svg);
            Assert.Contains("<svg", svg);
            Assert.Contains("</svg>", svg);

            string targetFile = Path.Combine(tempDir, "nested", "icon.svg");
            generator.SaveSvg(app, targetFile, 256, tempDir);

            Assert.True(File.Exists(targetFile));
            string savedContent = File.ReadAllText(targetFile);
            Assert.Contains("<svg", savedContent);
            Assert.Contains("</svg>", savedContent);
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    [Fact]
    public void SkiaIconRenderer_RenderIconSvg_RespectsGlyphTint_ForCustomGlyphSvg()
    {
        var renderer = new Catalyst.Adapters.Icons.SkiaIconRenderer();
        var app = new AppInfo
        {
            Name = "TintTestApp",
            Color = "#0000FF", // Blue background
            CustomGlyphSvg = @"<svg xmlns=""http://www.w3.org/2000/svg"" viewBox=""0 0 100 100""><rect x=""25"" y=""25"" width=""50"" height=""50"" fill=""#000000"" /></svg>",
            CustomGlyphColor = "#FF0000" // Red glyph tint
        };

        string svg = renderer.RenderIconSvg(app, 512, ".");
        Assert.NotNull(svg);

        // Load the rendered SVG and render to bitmap to check pixel color
        var skSvg = new Svg.Skia.SKSvg();
        skSvg.FromSvg(svg);
        Assert.NotNull(skSvg.Picture);

        using var bitmap = new SkiaSharp.SKBitmap(512, 512);
        using (var canvas = new SkiaSharp.SKCanvas(bitmap))
        {
            canvas.Clear(SkiaSharp.SKColors.Transparent);
            canvas.DrawPicture(skSvg.Picture);
        }

        // Center pixel (256, 256) is inside the glyph rect and must be red (#FF0000), NOT black (#000000)
        var centerPixel = bitmap.GetPixel(256, 256);
        Assert.True(centerPixel.Red > 200, $"Expected center glyph to be red, but Red was {centerPixel.Red} (Full color: {centerPixel})");
        Assert.True(centerPixel.Blue < 50, $"Expected center glyph to not be blue, but Blue was {centerPixel.Blue}");
        Assert.True(centerPixel.Green < 50, $"Expected center glyph to not be green, but Green was {centerPixel.Green}");
    }

    [Fact]
    public void SkiaIconRenderer_RenderIconSvg_RespectsGlyphTint_ForBootstrapIcon()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "CatalystBootstrapTintTest_" + Guid.NewGuid().ToString("N"));
        string cacheDir = Path.Combine(tempDir, "_bootstrap_cache");
        Directory.CreateDirectory(cacheDir);

        try
        {
            // Standard Bootstrap icon with fill="currentColor"
            File.WriteAllText(Path.Combine(cacheDir, "testicon.svg"),
                @"<svg xmlns=""http://www.w3.org/2000/svg"" width=""16"" height=""16"" fill=""currentColor"" viewBox=""0 0 16 16""><rect x=""4"" y=""4"" width=""8"" height=""8"" /></svg>");

            var renderer = new Catalyst.Adapters.Icons.SkiaIconRenderer();
            var app = new AppInfo
            {
                Name = "BootstrapTintApp",
                Color = "#0000FF", // Blue background
                BootstrapIcon = "testicon",
                CustomGlyphColor = "#00FF00" // Green glyph tint
            };

            string svg = renderer.RenderIconSvg(app, 512, tempDir);
            Assert.NotNull(svg);

            var skSvg = new Svg.Skia.SKSvg();
            skSvg.FromSvg(svg);
            Assert.NotNull(skSvg.Picture);

            using var bitmap = new SkiaSharp.SKBitmap(512, 512);
            using (var canvas = new SkiaSharp.SKCanvas(bitmap))
            {
                canvas.Clear(SkiaSharp.SKColors.Transparent);
                canvas.DrawPicture(skSvg.Picture);
            }

            // Center pixel (256, 256) is inside the glyph rect and must be green (#00FF00), NOT black (#000000) or white
            var centerPixel = bitmap.GetPixel(256, 256);
            Assert.True(centerPixel.Green > 200, $"Expected center glyph to be green, but Green was {centerPixel.Green} (Full color: {centerPixel})");
            Assert.True(centerPixel.Red < 50, $"Expected center glyph to not be red, but Red was {centerPixel.Red}");
            Assert.True(centerPixel.Blue < 50, $"Expected center glyph to not be blue, but Blue was {centerPixel.Blue}");
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    [Fact]
    public void SkiaIconRenderer_RenderIconSvg_BootstrapIconDefaultsToWhite_NotBlack()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "CatalystBootstrapWhiteTest_" + Guid.NewGuid().ToString("N"));
        string cacheDir = Path.Combine(tempDir, "_bootstrap_cache");
        Directory.CreateDirectory(cacheDir);

        try
        {
            File.WriteAllText(Path.Combine(cacheDir, "whiteicon.svg"),
                @"<svg xmlns=""http://www.w3.org/2000/svg"" width=""16"" height=""16"" fill=""currentColor"" viewBox=""0 0 16 16""><rect x=""4"" y=""4"" width=""8"" height=""8"" /></svg>");

            var renderer = new Catalyst.Adapters.Icons.SkiaIconRenderer();
            var app = new AppInfo
            {
                Name = "BootstrapWhiteApp",
                Color = "#0000FF", // Blue background
                BootstrapIcon = "whiteicon"
                // CustomGlyphColor not set -> default is white
            };

            string svg = renderer.RenderIconSvg(app, 512, tempDir);
            Assert.NotNull(svg);

            var skSvg = new Svg.Skia.SKSvg();
            skSvg.FromSvg(svg);
            Assert.NotNull(skSvg.Picture);

            using var bitmap = new SkiaSharp.SKBitmap(512, 512);
            using (var canvas = new SkiaSharp.SKCanvas(bitmap))
            {
                canvas.Clear(SkiaSharp.SKColors.Transparent);
                canvas.DrawPicture(skSvg.Picture);
            }

            // Center pixel must be White (#FFFFFF), NOT black (#000000)
            var centerPixel = bitmap.GetPixel(256, 256);
            Assert.True(centerPixel.Red > 200, $"Expected center glyph to be white (Red > 200), but Red was {centerPixel.Red} (Full color: {centerPixel})");
            Assert.True(centerPixel.Green > 200, $"Expected center glyph to be white (Green > 200), but Green was {centerPixel.Green}");
            Assert.True(centerPixel.Blue > 200, $"Expected center glyph to be white (Blue > 200), but Blue was {centerPixel.Blue}");
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    [Fact]
    public void AppConfigurationService_ConvertToAppInfo_AutomaticallyPopulatesIconPathFromDiskIfExists()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "CatalystIconPathTest_" + Guid.NewGuid().ToString("N"));
        string iconsDir = Path.Combine(tempDir, "icons", "AutoPathApp");
        Directory.CreateDirectory(iconsDir);

        try
        {
            string expectedPngPath = Path.Combine(iconsDir, "AutoPathApp.png");
            File.WriteAllText(expectedPngPath, "dummy png content");

            var mockConfigRepo = new Catalyst.Adapters.Persistence.YamlConfigRepository();
            var mockSettings = new Catalyst.Adapters.Persistence.JsonSettingsStorage(Path.Combine(tempDir, "settings.json"));
            var mockPathResolver = new Catalyst.Adapters.Persistence.PathResolver(mockSettings);
            mockPathResolver.CustomConfigPath = Path.Combine(tempDir, "catalyst.yaml");
            var configService = new Catalyst.Core.Services.AppConfigurationService(mockConfigRepo, mockSettings, mockPathResolver);

            var entry = new Catalyst.AppConfigEntry
            {
                Name = "AutoPathApp",
                Icon = new Catalyst.IconConfig
                {
                    Color = "#FF0000",
                    IconPath = null // Not explicitly provided in YAML
                }
            };

            var appInfo = configService.ConvertToAppInfo(entry, tempDir, Path.Combine(tempDir, "icons"));

            Assert.Equal("AutoPathApp", appInfo.Name);
            Assert.Equal(expectedPngPath, appInfo.IconPath);
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    [Fact]
    public void AppConfigurationService_SaveAndReload_PreservesGeneratedIconPathAndChangesWithoutRestart()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "CatalystSaveReloadTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var mockConfigRepo = new Catalyst.Adapters.Persistence.YamlConfigRepository();
            var mockSettings = new Catalyst.Adapters.Persistence.JsonSettingsStorage(Path.Combine(tempDir, "settings.json"));
            var mockPathResolver = new Catalyst.Adapters.Persistence.PathResolver(mockSettings);
            string configPath = Path.Combine(tempDir, "catalyst.yaml");
            mockPathResolver.CustomConfigPath = configPath;
            var configService = new Catalyst.Core.Services.AppConfigurationService(mockConfigRepo, mockSettings, mockPathResolver);

            var renderer = new Catalyst.Adapters.Icons.SkiaIconRenderer();
            var iconService = new Catalyst.Core.Services.IconManagementService(renderer, configService);

            var app = new AppInfo
            {
                Name = "ReloadTestApp",
                Color = "#FF1234",
                SecondaryColor = "#567890",
                BackgroundType = "Gradient",
                Label = "RL"
            };

            // Generate icon
            iconService.GenerateIcon(app);
            string generatedPng = Path.Combine(tempDir, "icons", "ReloadTestApp", "ReloadTestApp.png");
            Assert.True(File.Exists(generatedPng));

            app.IconPath = generatedPng;

            // Save config
            configService.SaveApps(new[] { app }, configPath);

            // Reload config as if MainWindow or ViewModel reloaded without restart
            var loadedApps = configService.LoadApps(configPath);
            Assert.Single(loadedApps);
            var reloadedApp = loadedApps[0];

            Assert.Equal("ReloadTestApp", reloadedApp.Name);
            Assert.Equal("#FF1234", reloadedApp.Color);
            Assert.Equal("#567890", reloadedApp.SecondaryColor);
            Assert.Equal("Gradient", reloadedApp.BackgroundType);
            Assert.Equal(generatedPng, reloadedApp.IconPath);
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    [Fact]
    public void MainWindowViewModel_LoadApps_ShowsOnlyGeneratedIconsFromDisk()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "CatalystMainVmTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var mockConfigRepo = new Catalyst.Adapters.Persistence.YamlConfigRepository();
            var mockSettings = new Catalyst.Adapters.Persistence.JsonSettingsStorage(Path.Combine(tempDir, "settings.json"));
            var mockPathResolver = new Catalyst.Adapters.Persistence.PathResolver(mockSettings);
            string configPath = Path.Combine(tempDir, "catalyst.yaml");
            mockPathResolver.CustomConfigPath = configPath;
            var configService = new Catalyst.Core.Services.AppConfigurationService(mockConfigRepo, mockSettings, mockPathResolver);

            var renderer = new Catalyst.Adapters.Icons.SkiaIconRenderer();
            var iconService = new Catalyst.Core.Services.IconManagementService(renderer, configService);

            var entry = new Catalyst.AppConfigEntry
            {
                Name = "NoIconApp",
                Icon = new Catalyst.IconConfig
                {
                    Color = "#2288CC",
                    Label = "NI"
                }
            };
            var configFile = new Catalyst.AppConfigFile();
            configFile.Apps.Add(entry);
            mockConfigRepo.SaveConfigFile(configFile, configPath);

            var launcher = new Catalyst.Core.Services.AppLauncherService(new Catalyst.Adapters.Processes.WindowsProcessExecutor(), configService);
            var hotkeyHook = new Catalyst.Adapters.Platform.WindowsHotkeyHook();
            var hotkey = new Catalyst.Core.Services.HotkeyService(hotkeyHook);
            var windowPlacement = new Catalyst.Adapters.Platform.WindowPlacementService();
            var windowService = new Catalyst.Adapters.UI.Navigation.WindowService(null!, windowPlacement);

            var mainVm = new Catalyst.Adapters.UI.ViewModels.MainWindowViewModel(configService, launcher, hotkey, windowService, iconService);
            mainVm.LoadApps(configPath);

            Assert.Single(mainVm.Apps);
            var app = mainVm.Apps[0];
            Assert.Null(app.IconSource);
            Assert.Null(app.IconSource32);

            // Generate icon
            iconService.GenerateIcon(app);
            string iconPath = Path.Combine(tempDir, "icons", app.Name, $"{app.Name}.png");
            app.IconPath = iconPath;
            app.RefreshIcons();

            Assert.NotNull(app.IconSource);
            Assert.NotNull(app.IconSource32);
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    [Fact]
    public void AppManagementViewModel_InitializeAndGenerateAll_GeneratesIconsAndPreservesVisuals()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "CatalystAppMgmtTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var mockConfigRepo = new Catalyst.Adapters.Persistence.YamlConfigRepository();
            var mockSettings = new Catalyst.Adapters.Persistence.JsonSettingsStorage(Path.Combine(tempDir, "settings.json"));
            var mockPathResolver = new Catalyst.Adapters.Persistence.PathResolver(mockSettings);
            string configPath = Path.Combine(tempDir, "catalyst.yaml");
            mockPathResolver.CustomConfigPath = configPath;
            var configService = new Catalyst.Core.Services.AppConfigurationService(mockConfigRepo, mockSettings, mockPathResolver);

            var renderer = new Catalyst.Adapters.Icons.SkiaIconRenderer();
            var iconService = new Catalyst.Core.Services.IconManagementService(renderer, configService);
            var hotkeyHook = new Catalyst.Adapters.Platform.WindowsHotkeyHook();
            var hotkey = new Catalyst.Core.Services.HotkeyService(hotkeyHook);
            var windowPlacement = new Catalyst.Adapters.Platform.WindowPlacementService();
            var windowService = new Catalyst.Adapters.UI.Navigation.WindowService(null!, windowPlacement);

            var app1 = new AppInfo { Name = "GenApp1", Color = "#FF0000", Label = "G1" };
            var app2 = new AppInfo { Name = "GenApp2", Color = "#00FF00", Label = "G2" };

            configService.SaveApps(new[] { app1, app2 }, configPath);

            var mgmtVm = new Catalyst.Adapters.UI.ViewModels.AppManagementViewModel(configService, iconService, hotkey, windowService);
            mgmtVm.ConfigFilePath = configPath;
            mgmtVm.Initialize(null, "Alt+Space", configPath);

            Assert.Equal(2, mgmtVm.Apps.Count);
            Assert.Null(mgmtVm.Apps[0].IconSource32);
            Assert.Null(mgmtVm.Apps[1].IconSource32);

            // Render live preview for selected app
            var livePreview = mgmtVm.RenderPreview(mgmtVm.Apps[0]);
            mgmtVm.Apps[0].LivePreviewSource = livePreview;
            Assert.NotNull(mgmtVm.Apps[0].LivePreviewSource);
            Assert.Null(mgmtVm.Apps[0].IconSource32);

            // Generate all icons
            mgmtVm.GenerateAllIcons();

            string icon1Path = Path.Combine(tempDir, "icons", "GenApp1", "GenApp1.png");
            string icon2Path = Path.Combine(tempDir, "icons", "GenApp2", "GenApp2.png");
            string svg1Path = Path.Combine(tempDir, "icons", "GenApp1", "GenApp1.svg");
            string ico1Path = Path.Combine(tempDir, "icons", "GenApp1", "favicon.ico");

            Assert.True(File.Exists(icon1Path));
            Assert.True(File.Exists(icon2Path));
            Assert.True(File.Exists(svg1Path));
            Assert.True(File.Exists(ico1Path));

            // Icons should be loaded into AppInfo
            mgmtVm.Apps[0].IconPath = icon1Path;
            mgmtVm.Apps[1].IconPath = icon2Path;
            mgmtVm.Apps[0].RefreshIcons();
            mgmtVm.Apps[1].RefreshIcons();

            Assert.NotNull(mgmtVm.Apps[0].IconSource32);
            Assert.NotNull(mgmtVm.Apps[1].IconSource32);
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    [Fact]
    public void AppInfo_LivePreviewVsGeneratedIcon_RemainIndependent()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "CatalystLiveGenTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var mockConfigRepo = new Catalyst.Adapters.Persistence.YamlConfigRepository();
            var mockSettings = new Catalyst.Adapters.Persistence.JsonSettingsStorage(Path.Combine(tempDir, "settings.json"));
            var mockPathResolver = new Catalyst.Adapters.Persistence.PathResolver(mockSettings);
            string configPath = Path.Combine(tempDir, "catalyst.yaml");
            mockPathResolver.CustomConfigPath = configPath;
            var configService = new Catalyst.Core.Services.AppConfigurationService(mockConfigRepo, mockSettings, mockPathResolver);

            var renderer = new Catalyst.Adapters.Icons.SkiaIconRenderer();
            var iconService = new Catalyst.Core.Services.IconManagementService(renderer, configService);

            var app = new AppInfo { Name = "LiveApp", Color = "#112233", Label = "LA" };
            iconService.GenerateIcon(app);

            string initialIconPath = Path.Combine(tempDir, "icons", "LiveApp", "LiveApp.png");
            Assert.True(File.Exists(initialIconPath));
            app.IconPath = initialIconPath;
            app.RefreshIcons();

            Assert.NotNull(app.IconSource);
            Assert.NotNull(app.IconSource32);

            // User modifies live settings (e.g. changes color and glyph)
            app.Color = "#FF00FF";
            app.BootstrapIcon = "bi-gear";
            var newLivePreview = iconService.RenderPreview(app, 512);
            app.LivePreviewSource = newLivePreview;
            app.LivePreviewSource48 = iconService.RenderPreview(app, 48);
            app.LivePreviewSource32 = iconService.RenderPreview(app, 32);
            app.LivePreviewSource16 = iconService.RenderPreview(app, 16);

            // Live preview is updated
            Assert.NotNull(app.LivePreviewSource);
            Assert.NotNull(app.LivePreviewSource48);
            Assert.NotNull(app.LivePreviewSource32);
            Assert.NotNull(app.LivePreviewSource16);

            // Generated icon is STILL the original generated icon from disk, untouched
            Assert.NotNull(app.IconSource);
            Assert.NotNull(app.IconSource32);
            Assert.Equal(initialIconPath, app.IconPath);
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    [Fact]
    public void AppInfo_GeneratedIconProperties_ReflectNullWhenNotGenerated()
    {
        var app = new AppInfo { Name = "UngeneratedApp", Color = "#123456" };
        Assert.False(app.HasGeneratedIcon);
        Assert.Null(app.IconSource);
        Assert.Null(app.IconSource16);
        Assert.Null(app.IconSource32);
        Assert.Null(app.IconSource48);
        Assert.Null(app.LivePreviewSource);
    }

    [Fact]
    public void AppInfo_LivePreviewProperties_FallbackCorrectly()
    {
        var app = new AppInfo { Name = "PreviewFallbackApp" };
        var renderer = new Catalyst.Adapters.Icons.SkiaIconRenderer();
        var preview = renderer.RenderPreview(app, "icons", null, 512);
        Assert.NotNull(preview);

        app.LivePreviewSource = preview;
        Assert.NotNull(app.LivePreviewSource);
        Assert.NotNull(app.PreviewSource);
        Assert.NotNull(app.LivePreviewSource48);
        Assert.NotNull(app.LivePreviewSource32);
        Assert.NotNull(app.LivePreviewSource16);

        // IconSource remains null
        Assert.Null(app.IconSource);
        Assert.Null(app.IconSource16);
        Assert.Null(app.IconSource32);
        Assert.Null(app.IconSource48);
    }

    [Fact]
    public void SettingsViewModel_LoadSaveResetAndValidate_WorksCorrectly()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "CatalystSettingsWindowTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var mockConfigRepo = new Catalyst.Adapters.Persistence.YamlConfigRepository();
            var mockSettings = new Catalyst.Adapters.Persistence.JsonSettingsStorage(Path.Combine(tempDir, "settings.json"));
            var mockPathResolver = new Catalyst.Adapters.Persistence.PathResolver(mockSettings);
            string configPath = Path.Combine(tempDir, "catalyst.yaml");
            mockPathResolver.CustomConfigPath = configPath;
            var configService = new Catalyst.Core.Services.AppConfigurationService(mockConfigRepo, mockSettings, mockPathResolver);

            var hotkeyHook = new Catalyst.Adapters.Platform.WindowsHotkeyHook();
            var hotkey = new Catalyst.Core.Services.HotkeyService(hotkeyHook);

            var app = new AppInfo { Name = "SettingsApp", Color = "#336699", Label = "SA" };
            configService.SaveApps(new[] { app }, "Ctrl+Alt+S", configPath);

            var settingsVm = new Catalyst.Adapters.UI.ViewModels.SettingsViewModel(configService, hotkey);
            Assert.Equal(configPath, settingsVm.ConfigFilePath);
            Assert.Equal("Ctrl+Alt+S", settingsVm.ConfiguredHotkey);

            // Test hotkey validation
            Assert.True(settingsVm.ValidateHotkey("Alt+Space"));
            Assert.True(settingsVm.ValidateHotkey("Ctrl+Shift+F12"));
            Assert.False(settingsVm.ValidateHotkey("InvalidKeyCombo123"));

            // Change hotkey and save
            settingsVm.ConfiguredHotkey = "Ctrl+Shift+Z";
            settingsVm.Save();

            // Verify saved
            var reloadedVm = new Catalyst.Adapters.UI.ViewModels.SettingsViewModel(configService, hotkey);
            reloadedVm.LoadConfig(configPath);
            Assert.Equal("Ctrl+Shift+Z", reloadedVm.ConfiguredHotkey);

            // Test Reset to Default
            settingsVm.ResetToDefault();
            Assert.Null(configService.GetCustomConfigPath());
            Assert.Null(configService.GetCustomIconsDir());

            // Test setting custom IconsDirectory
            string customIconsDir = Path.Combine(tempDir, "custom_icons_output");
            settingsVm.IconsDirectory = customIconsDir;
            settingsVm.Save();

            Assert.Equal(Path.GetFullPath(customIconsDir), configService.IconsBaseDir);
            Assert.Equal(Path.GetFullPath(customIconsDir), configService.GetCustomIconsDir());

            // Reset icons directory
            settingsVm.ResetIconsDirectoryToDefault();
            Assert.Null(configService.GetCustomIconsDir());
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    [Fact]
    public void MainWindowViewModel_OpenSettings_CallsWindowServiceAndReloadsApps()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "CatalystMainSettingsTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var mockConfigRepo = new Catalyst.Adapters.Persistence.YamlConfigRepository();
            var mockSettings = new Catalyst.Adapters.Persistence.JsonSettingsStorage(Path.Combine(tempDir, "settings.json"));
            var mockPathResolver = new Catalyst.Adapters.Persistence.PathResolver(mockSettings);
            string configPath = Path.Combine(tempDir, "catalyst.yaml");
            mockPathResolver.CustomConfigPath = configPath;
            var configService = new Catalyst.Core.Services.AppConfigurationService(mockConfigRepo, mockSettings, mockPathResolver);

            var processExec = new Catalyst.Adapters.Processes.WindowsProcessExecutor();
            var launcherService = new Catalyst.Core.Services.AppLauncherService(processExec, configService);
            var hotkeyHook = new Catalyst.Adapters.Platform.WindowsHotkeyHook();
            var hotkey = new Catalyst.Core.Services.HotkeyService(hotkeyHook);
            var windowPlacement = new Catalyst.Adapters.Platform.WindowPlacementService();
            
            bool showSettingsCalled = false;
            var mockWindowService = new MockWindowService(() => showSettingsCalled = true);

            var app = new AppInfo { Name = "MainTestApp", Color = "#FF9900", Label = "MT" };
            configService.SaveApps(new[] { app }, configPath);

            var mainVm = new Catalyst.Adapters.UI.ViewModels.MainWindowViewModel(
                configService, launcherService, hotkey, mockWindowService);

            mainVm.LoadApps();
            Assert.Single(mainVm.Apps);

            mainVm.OpenSettings();
            Assert.True(showSettingsCalled);
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    [Fact]
    public void UpdateProjectFavicon_SingleApp_CopiesIcoAndUpdatesCsproj()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "CatalystFaviconTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var mockConfigRepo = new Catalyst.Adapters.Persistence.YamlConfigRepository();
            var mockSettings = new Catalyst.Adapters.Persistence.JsonSettingsStorage(Path.Combine(tempDir, "settings.json"));
            var mockPathResolver = new Catalyst.Adapters.Persistence.PathResolver(mockSettings);
            string configPath = Path.Combine(tempDir, "apps.yaml");
            mockPathResolver.CustomConfigPath = configPath;
            var configService = new Catalyst.Core.Services.AppConfigurationService(mockConfigRepo, mockSettings, mockPathResolver);

            var iconRenderer = new Catalyst.Adapters.Icons.SkiaIconRenderer();
            var iconService = new Catalyst.Core.Services.IconManagementService(iconRenderer, configService);
            var hotkeyHook = new Catalyst.Adapters.Platform.WindowsHotkeyHook();
            var hotkey = new Catalyst.Core.Services.HotkeyService(hotkeyHook);
            var mockWindowService = new MockWindowService(() => { });

            string projectDir = Path.Combine(tempDir, "MyTestApp");
            Directory.CreateDirectory(projectDir);
            string csprojPath = Path.Combine(projectDir, "MyTestApp.csproj");
            File.WriteAllText(csprojPath, "<Project Sdk=\"Microsoft.NET.Sdk\">\n  <PropertyGroup>\n    <OutputType>WinExe</OutputType>\n  </PropertyGroup>\n</Project>");

            string targetFaviconPath = Path.Combine(projectDir, "wwwroot", "favicon.ico");

            var app = new AppInfo
            {
                Name = "MyTestApp",
                Color = "#4A90E2",
                Label = "MTA",
                FaviconPath = targetFaviconPath,
                ProjectPath = csprojPath
            };

            configService.SaveApps(new[] { app }, configPath);

            var mgmtVm = new Catalyst.Adapters.UI.ViewModels.AppManagementViewModel(
                configService, iconService, hotkey, mockWindowService);

            // Test updating single app favicon via ViewModel
            bool success = mgmtVm.UpdateProjectFavicon(app);
            Assert.True(success);

            // Verify favicon.ico was created at target path
            Assert.True(File.Exists(targetFaviconPath));
            Assert.True(new FileInfo(targetFaviconPath).Length > 0);

            // Verify csproj was updated with <ApplicationIcon>
            string updatedCsproj = File.ReadAllText(csprojPath);
            Assert.Contains("<ApplicationIcon>", updatedCsproj);
            Assert.Contains("favicon.ico", updatedCsproj);

            // Test without favicon path returns false
            var emptyFaviconApp = new AppInfo { Name = "EmptyFaviconApp", Color = "#112233" };
            Assert.False(mgmtVm.UpdateProjectFavicon(emptyFaviconApp));
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    [Fact]
    public void UpdateProjectFavicon_PngAndSvgTargets_CopiesFilesCorrectly()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "CatalystFaviconPngSvgTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var mockConfigRepo = new Catalyst.Adapters.Persistence.YamlConfigRepository();
            var mockSettings = new Catalyst.Adapters.Persistence.JsonSettingsStorage(Path.Combine(tempDir, "settings.json"));
            var mockPathResolver = new Catalyst.Adapters.Persistence.PathResolver(mockSettings);
            string configPath = Path.Combine(tempDir, "apps.yaml");
            mockPathResolver.CustomConfigPath = configPath;
            var configService = new Catalyst.Core.Services.AppConfigurationService(mockConfigRepo, mockSettings, mockPathResolver);

            var iconRenderer = new Catalyst.Adapters.Icons.SkiaIconRenderer();
            var iconService = new Catalyst.Core.Services.IconManagementService(iconRenderer, configService);

            string targetPngPath = Path.Combine(tempDir, "web", "favicon.png");
            string targetSvgPath = Path.Combine(tempDir, "web", "app-icon.svg");

            var appPng = new AppInfo
            {
                Name = "PngApp",
                Color = "#FF5722",
                Label = "PNG",
                FaviconPath = targetPngPath
            };

            var appSvg = new AppInfo
            {
                Name = "SvgApp",
                Color = "#4CAF50",
                Label = "SVG",
                FaviconPath = targetSvgPath
            };

            Assert.True(iconService.UpdateProjectFavicon(appPng));
            Assert.True(File.Exists(targetPngPath));
            Assert.True(new FileInfo(targetPngPath).Length > 0);

            Assert.True(iconService.UpdateProjectFavicon(appSvg));
            Assert.True(File.Exists(targetSvgPath));
            Assert.True(new FileInfo(targetSvgPath).Length > 0);
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    [Fact]
    public void Settings_SetCustomConfigFile_PersistsAndLoadsAcrossAppRestart()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "CatalystSettingsRestartTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        string settingsFile = Path.Combine(tempDir, "settings.json");
        string customYamlFile = Path.Combine(tempDir, "custom_apps.yaml");

        try
        {
            // 1. Create a custom YAML file with apps
            var configRepo = new Catalyst.Adapters.Persistence.YamlConfigRepository();
            var settingsStorage = new Catalyst.Adapters.Persistence.JsonSettingsStorage(settingsFile);
            var pathResolver = new Catalyst.Adapters.Persistence.PathResolver(settingsStorage);
            var configService = new Catalyst.Core.Services.AppConfigurationService(configRepo, settingsStorage, pathResolver);
            var hotkeyHook = new Catalyst.Adapters.Platform.WindowsHotkeyHook();
            var hotkeyService = new Catalyst.Core.Services.HotkeyService(hotkeyHook);

            var sampleApp = new AppInfo { Name = "PersistentApp", Color = "#123456", Label = "PA" };
            configService.SaveApps(new[] { sampleApp }, "Ctrl+Shift+P", customYamlFile);

            // 2. Open Settings ViewModel and set the custom config path
            var settingsVm = new Catalyst.Adapters.UI.ViewModels.SettingsViewModel(configService, hotkeyService);
            settingsVm.ConfigFilePath = customYamlFile;
            settingsVm.Save();

            Assert.Equal(customYamlFile, settingsVm.ConfigFilePath);
            Assert.Equal(customYamlFile, configService.ActiveConfigPath);

            // 3. Simulate App Restart: Create completely fresh instances of all services pointing to the same settings.json
            var restartedSettingsStorage = new Catalyst.Adapters.Persistence.JsonSettingsStorage(settingsFile);
            var restartedPathResolver = new Catalyst.Adapters.Persistence.PathResolver(restartedSettingsStorage);
            var restartedConfigRepo = new Catalyst.Adapters.Persistence.YamlConfigRepository();
            var restartedConfigService = new Catalyst.Core.Services.AppConfigurationService(restartedConfigRepo, restartedSettingsStorage, restartedPathResolver);

            // Verify the active config path is automatically the custom YAML file
            Assert.Equal(Path.GetFullPath(customYamlFile), restartedConfigService.ActiveConfigPath);

            // Verify loading apps loads the app from the custom YAML file, NOT an empty/default config
            var reloadedApps = restartedConfigService.LoadApps();
            Assert.Single(reloadedApps);
            Assert.Equal("PersistentApp", reloadedApps[0].Name);
            Assert.Equal("#123456", reloadedApps[0].Color);
            Assert.Equal("PA", reloadedApps[0].Label);
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    [Fact]
    public void YamlConfigRepository_LoadsDifferentNamingConventionsAndPreservesProperties()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "CatalystYamlConventionsTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        string yamlSnakeCase = Path.Combine(tempDir, "snake_case.yaml");

        try
        {
            string snakeCaseContent = """
            hotkey: Alt+Space
            apps:
              - name: Warpdeck
                hidden: false
                launch:
                  project_path: C:\Projects\Warpdeck\Warpdeck.csproj
                  executable_path: C:\Projects\Warpdeck\bin\Warpdeck.exe
                  working_directory: C:\Projects\Warpdeck
                  arguments: --dev
                  run_as_admin: true
                icon:
                  color: "#1E88E4"
                  secondary_color: "#FF5500"
                  background_type: Gradient
                  gradient_direction: Diagonal
                  label: WD
                  icon_path: C:\Projects\Warpdeck\icon.png
                  favicon_path: C:\Projects\Warpdeck\favicon.ico
                  bootstrap_icon: rocket
                  custom_glyph_svg: <svg>test</svg>
                  custom_glyph_color: "#FFFFFF"
                  svg_override: <svg>override</svg>
            """;
            File.WriteAllText(yamlSnakeCase, snakeCaseContent);

            var repo = new Catalyst.Adapters.Persistence.YamlConfigRepository();
            var config = repo.LoadConfigFile(yamlSnakeCase);

            Assert.NotNull(config);
            Assert.Single(config.Apps);
            var app = config.Apps[0];
            Assert.Equal("Warpdeck", app.Name);
            Assert.Equal(@"C:\Projects\Warpdeck\Warpdeck.csproj", app.Launch.ProjectPath);
            Assert.Equal(@"C:\Projects\Warpdeck\bin\Warpdeck.exe", app.Launch.ExecutablePath);
            Assert.Equal(@"C:\Projects\Warpdeck", app.Launch.WorkingDirectory);
            Assert.Equal("--dev", app.Launch.Arguments);
            Assert.True(app.Launch.RunAsAdmin);

            Assert.Equal("#1E88E4", app.Icon.Color);
            Assert.Equal("#FF5500", app.Icon.SecondaryColor);
            Assert.Equal("Gradient", app.Icon.BackgroundType);
            Assert.Equal("Diagonal", app.Icon.GradientDirection);
            Assert.Equal("WD", app.Icon.Label);
            Assert.Equal(@"C:\Projects\Warpdeck\icon.png", app.Icon.IconPath);
            Assert.Equal(@"C:\Projects\Warpdeck\favicon.ico", app.Icon.FaviconPath);
            Assert.Equal("rocket", app.Icon.BootstrapIcon);
            Assert.Equal("<svg>test</svg>", app.Icon.CustomGlyphSvg);
            Assert.Equal("#FFFFFF", app.Icon.CustomGlyphColor);
            Assert.Equal("<svg>override</svg>", app.Icon.SvgOverride);
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    [Fact]
    public void PathResolver_NormalizePath_HandlesQuotesTildeEnvVarsAndDirectories()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "CatalystNormalizeTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            string expectedFile = Path.Combine(tempDir, "apps.yaml");

            // 1. Quoted path
            string quoted = $"\"{expectedFile}\"";
            Assert.Equal(expectedFile, Catalyst.Adapters.Persistence.PathResolver.NormalizePath(quoted));

            // 2. Single-quoted path
            string singleQuoted = $"'{expectedFile}'";
            Assert.Equal(expectedFile, Catalyst.Adapters.Persistence.PathResolver.NormalizePath(singleQuoted));

            // 3. Directory path (should append apps.yaml)
            Assert.Equal(expectedFile, Catalyst.Adapters.Persistence.PathResolver.NormalizePath(tempDir));
            Assert.Equal(expectedFile, Catalyst.Adapters.Persistence.PathResolver.NormalizePath(tempDir + Path.DirectorySeparatorChar));

            // 4. Environment variable
            Environment.SetEnvironmentVariable("CATALYST_TEST_TEMP", tempDir);
            string envPath = "%CATALYST_TEST_TEMP%\\apps.yaml";
            Assert.Equal(expectedFile, Catalyst.Adapters.Persistence.PathResolver.NormalizePath(envPath));
            Environment.SetEnvironmentVariable("CATALYST_TEST_TEMP", null);
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    [Fact]
    public void JsonSettingsStorage_LoadsAlternateNamingAndToleratesComments()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "CatalystSettingsAltTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        string settingsFile = Path.Combine(tempDir, "settings.json");

        try
        {
            // JSON with snake_case property names, comments, and trailing comma
            string jsonContent = """
            {
                // User settings configuration
                "config_path": "C:\\MyProjects\\apps.yaml",
                "hot_key": "Ctrl+Space",
            }
            """;
            File.WriteAllText(settingsFile, jsonContent);

            var storage = new Catalyst.Adapters.Persistence.JsonSettingsStorage(settingsFile);
            var loaded = storage.LoadSettings();

            Assert.NotNull(loaded);
            Assert.Equal(@"C:\MyProjects\apps.yaml", loaded.ConfigPath);
            Assert.Equal("Ctrl+Space", loaded.Hotkey);
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    [Fact]
    public void SettingsPersistence_QuotedAndDirectoryPaths_PersistAcrossRestarts()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "CatalystRestartQuotedTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        string settingsFile = Path.Combine(tempDir, "settings.json");
        string customYamlFile = Path.Combine(tempDir, "custom-apps.yaml");

        try
        {
            var settingsStorage = new Catalyst.Adapters.Persistence.JsonSettingsStorage(settingsFile);
            var pathResolver = new Catalyst.Adapters.Persistence.PathResolver(settingsStorage);
            var configRepo = new Catalyst.Adapters.Persistence.YamlConfigRepository();
            var configService = new Catalyst.Core.Services.AppConfigurationService(configRepo, settingsStorage, pathResolver);
            var hotkeyHook = new Catalyst.Adapters.Platform.WindowsHotkeyHook();
            var hotkeyService = new Catalyst.Core.Services.HotkeyService(hotkeyHook);

            var sampleApp = new AppInfo { Name = "QuotedPathApp", Color = "#654321", Label = "QA" };
            configService.SaveApps(new[] { sampleApp }, "Alt+Space", customYamlFile);

            // User pasted path with surrounding quotes into SettingsViewModel
            var settingsVm = new Catalyst.Adapters.UI.ViewModels.SettingsViewModel(configService, hotkeyService);
            settingsVm.ConfigFilePath = $"\"{customYamlFile}\"";
            settingsVm.Save();

            // Simulate app restart
            var restartedSettingsStorage = new Catalyst.Adapters.Persistence.JsonSettingsStorage(settingsFile);
            var restartedPathResolver = new Catalyst.Adapters.Persistence.PathResolver(restartedSettingsStorage);
            var restartedConfigRepo = new Catalyst.Adapters.Persistence.YamlConfigRepository();
            var restartedConfigService = new Catalyst.Core.Services.AppConfigurationService(restartedConfigRepo, restartedSettingsStorage, restartedPathResolver);

            Assert.Equal(Path.GetFullPath(customYamlFile), restartedConfigService.ActiveConfigPath);
            var reloadedApps = restartedConfigService.LoadApps();
            Assert.Single(reloadedApps);
            Assert.Equal("QuotedPathApp", reloadedApps[0].Name);
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    [Fact]
    public void ColorPicker_RgbToHsv_And_HsvToRgb_RoundTripCorrectly()
    {
        // Pure Red
        var (hRed, sRed, vRed) = Catalyst.Windows.ColorPickerWindow.RgbToHsv(255, 0, 0);
        Assert.Equal(0.0, hRed, 1);
        Assert.Equal(1.0, sRed, 2);
        Assert.Equal(1.0, vRed, 2);
        var (r1, g1, b1) = Catalyst.Windows.ColorPickerWindow.HsvToRgb(hRed, sRed, vRed);
        Assert.Equal(255, r1);
        Assert.Equal(0, g1);
        Assert.Equal(0, b1);

        // Pure Green
        var (hGreen, sGreen, vGreen) = Catalyst.Windows.ColorPickerWindow.RgbToHsv(0, 255, 0);
        Assert.Equal(120.0, hGreen, 1);
        Assert.Equal(1.0, sGreen, 2);
        Assert.Equal(1.0, vGreen, 2);
        var (r2, g2, b2) = Catalyst.Windows.ColorPickerWindow.HsvToRgb(hGreen, sGreen, vGreen);
        Assert.Equal(0, r2);
        Assert.Equal(255, g2);
        Assert.Equal(0, b2);

        // Pure Blue
        var (hBlue, sBlue, vBlue) = Catalyst.Windows.ColorPickerWindow.RgbToHsv(0, 0, 255);
        Assert.Equal(240.0, hBlue, 1);
        Assert.Equal(1.0, sBlue, 2);
        Assert.Equal(1.0, vBlue, 2);
        var (r3, g3, b3) = Catalyst.Windows.ColorPickerWindow.HsvToRgb(hBlue, sBlue, vBlue);
        Assert.Equal(0, r3);
        Assert.Equal(0, g3);
        Assert.Equal(255, b3);

        // White
        var (hWhite, sWhite, vWhite) = Catalyst.Windows.ColorPickerWindow.RgbToHsv(255, 255, 255);
        Assert.Equal(0.0, sWhite, 2);
        Assert.Equal(1.0, vWhite, 2);
        var (rw, gw, bw) = Catalyst.Windows.ColorPickerWindow.HsvToRgb(hWhite, sWhite, vWhite);
        Assert.Equal(255, rw);
        Assert.Equal(255, gw);
        Assert.Equal(255, bw);

        // Black
        var (hBlack, sBlack, vBlack) = Catalyst.Windows.ColorPickerWindow.RgbToHsv(0, 0, 0);
        Assert.Equal(0.0, vBlack, 2);
        var (rb, gb, bb) = Catalyst.Windows.ColorPickerWindow.HsvToRgb(hBlack, sBlack, vBlack);
        Assert.Equal(0, rb);
        Assert.Equal(0, gb);
        Assert.Equal(0, bb);

        // Brand Blue #1E88E4 (30, 136, 228)
        var (hBrand, sBrand, vBrand) = Catalyst.Windows.ColorPickerWindow.RgbToHsv(30, 136, 228);
        var (rBrand, gBrand, bBrand) = Catalyst.Windows.ColorPickerWindow.HsvToRgb(hBrand, sBrand, vBrand);
        Assert.InRange(rBrand, 29, 31);
        Assert.InRange(gBrand, 135, 137);
        Assert.InRange(bBrand, 227, 229);
    }

    [Fact]
    public void ColorPicker_NormalizeHex_And_TryParseHex_WorkCorrectly()
    {
        Assert.Equal("#1E88E4", Catalyst.Windows.ColorPickerWindow.NormalizeHex("1E88E4"));
        Assert.Equal("#1E88E4", Catalyst.Windows.ColorPickerWindow.NormalizeHex("#1E88E4"));
        Assert.Equal("#1E88E4", Catalyst.Windows.ColorPickerWindow.NormalizeHex(null));
        Assert.Equal("#1E88E4", Catalyst.Windows.ColorPickerWindow.NormalizeHex("   "));

        Assert.True(Catalyst.Windows.ColorPickerWindow.TryParseHex("#FF5500", out var c1));
        Assert.Equal(255, c1.R);
        Assert.Equal(85, c1.G);
        Assert.Equal(0, c1.B);

        Assert.True(Catalyst.Windows.ColorPickerWindow.TryParseHex("336699", out var c2));
        Assert.Equal(51, c2.R);
        Assert.Equal(102, c2.G);
        Assert.Equal(153, c2.B);

        Assert.False(Catalyst.Windows.ColorPickerWindow.TryParseHex("InvalidHexColor", out _));
        Assert.False(Catalyst.Windows.ColorPickerWindow.TryParseHex("", out _));
    }

    [Fact]
    public void ColorPicker_GenerateWheelBitmap_CreatesValidAntialiasedWheel()
    {
        var bitmap = Catalyst.Windows.ColorPickerWindow.GenerateWheelBitmap(100);
        Assert.NotNull(bitmap);
        Assert.Equal(100, bitmap.PixelWidth);
        Assert.Equal(100, bitmap.PixelHeight);
        Assert.True(bitmap.IsFrozen);
    }

    [Fact]
    public void ColorPicker_Window_InitializesWithoutException()
    {
        Exception? threadEx = null;
        var thread = new System.Threading.Thread(() =>
        {
            try
            {
                if (System.Windows.Application.ResourceAssembly == null)
                {
                    System.Windows.Application.ResourceAssembly = typeof(Catalyst.App).Assembly;
                }
                var window = new Catalyst.Windows.ColorPickerWindow("#FF0055");
                Assert.NotNull(window);
                Assert.Equal("#FF0055", window.SelectedHex);
            }
            catch (Exception ex)
            {
                threadEx = ex;
            }
        });
        thread.SetApartmentState(System.Threading.ApartmentState.STA);
        thread.Start();
        thread.Join(5000);

        Assert.Null(threadEx);
    }

    [Fact]
    public void AppConfiguration_CatalystDirectory_SerializesAndDeserializesCorrectly()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "CatalystDirTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var configRepo = new Catalyst.Adapters.Persistence.YamlConfigRepository();
            var settings = new Catalyst.Adapters.Persistence.JsonSettingsStorage(Path.Combine(tempDir, "settings.json"));
            var pathResolver = new Catalyst.Adapters.Persistence.PathResolver(settings);
            string configPath = Path.Combine(tempDir, "apps.yaml");
            pathResolver.CustomConfigPath = configPath;
            var configService = new Catalyst.Core.Services.AppConfigurationService(configRepo, settings, pathResolver);

            string customCatalystDir = Path.Combine(tempDir, "FlightPlan", ".catalyst");

            var app = new AppInfo
            {
                Name = "FlightPlan",
                Color = "#1E88E4",
                Label = "FP",
                ProjectPath = Path.Combine(tempDir, "FlightPlan", "FlightPlan.csproj"),
                CatalystDirectory = customCatalystDir
            };

            configService.SaveApps(new[] { app }, configPath);

            string yamlContent = File.ReadAllText(configPath);
            Assert.Contains("catalystDirectory", yamlContent);

            var loadedApps = configService.LoadApps(configPath);
            Assert.Single(loadedApps);
            Assert.Equal("FlightPlan", loadedApps[0].Name);
            Assert.Equal(customCatalystDir, loadedApps[0].CatalystDirectory);
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    [Fact]
    public void ExportCatalystFolder_PackagesAllSettingsAndAssetsSuccessfully()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "CatalystExportTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var configRepo = new Catalyst.Adapters.Persistence.YamlConfigRepository();
            var settings = new Catalyst.Adapters.Persistence.JsonSettingsStorage(Path.Combine(tempDir, "settings.json"));
            var pathResolver = new Catalyst.Adapters.Persistence.PathResolver(settings);
            string configPath = Path.Combine(tempDir, "apps.yaml");
            pathResolver.CustomConfigPath = configPath;
            var configService = new Catalyst.Core.Services.AppConfigurationService(configRepo, settings, pathResolver);

            var iconRenderer = new Catalyst.Adapters.Icons.SkiaIconRenderer();
            var iconService = new Catalyst.Core.Services.IconManagementService(iconRenderer, configService);

            // Create a fake project with a custom glyph svg file
            string projDir = Path.Combine(tempDir, "Ghost");
            Directory.CreateDirectory(projDir);
            string csprojPath = Path.Combine(projDir, "Ghost.csproj");
            File.WriteAllText(csprojPath, "<Project Sdk=\"Microsoft.NET.Sdk\"/>");

            string customSvgFile = Path.Combine(tempDir, "custom-glyph.svg");
            File.WriteAllText(customSvgFile, "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 24 24\"><circle cx=\"12\" cy=\"12\" r=\"10\" fill=\"#FFF\"/></svg>");

            var app = new AppInfo
            {
                Name = "Ghost",
                Color = "#9C27B0",
                SecondaryColor = "#E91E63",
                BackgroundType = "Gradient",
                GradientDirection = "Diagonal",
                Label = "GH",
                ProjectPath = csprojPath,
                CustomGlyphSvg = customSvgFile,
                CustomGlyphColor = "#00FFFF",
                BootstrapIcon = "send"
            };

            string exportedDir = iconService.ExportCatalystFolder(app);

            // Verify exported folder location
            string expectedDir = Path.Combine(projDir, ".catalyst");
            Assert.Equal(expectedDir, exportedDir);
            Assert.True(Directory.Exists(exportedDir));

            // Verify catalystApp.yaml exists and contains settings
            string catalystAppYamlPath = Path.Combine(exportedDir, "catalystApp.yaml");
            Assert.True(File.Exists(catalystAppYamlPath));
            Assert.False(File.Exists(Path.Combine(exportedDir, "app.yaml")));
            Assert.False(File.Exists(Path.Combine(exportedDir, "catalyst.yaml")));

            string appYamlText = File.ReadAllText(catalystAppYamlPath);
            Assert.Contains("name: Ghost", appYamlText);
            Assert.Contains("color: '#9C27B0'", appYamlText);
            Assert.Contains("secondaryColor: '#E91E63'", appYamlText);
            Assert.Contains("backgroundType: Gradient", appYamlText);
            Assert.Contains("customGlyphSvg: assets/custom-glyph.svg", appYamlText);
            Assert.Contains("customGlyphColor: '#00FFFF'", appYamlText);
            Assert.Contains("bootstrapIcon: send", appYamlText);

            // Verify copied asset in assets/
            string copiedSvgPath = Path.Combine(exportedDir, "assets", "custom-glyph.svg");
            Assert.True(File.Exists(copiedSvgPath));
            Assert.Equal(File.ReadAllText(customSvgFile), File.ReadAllText(copiedSvgPath));

            // Verify output icons folder is NOT created in .catalyst
            Assert.False(Directory.Exists(Path.Combine(exportedDir, "icons")));
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    [Fact]
    public void ExportCatalystFolder_ViaViewModel_WithInlineSvgAndCustomDirectory()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "CatalystVmMiniTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var configRepo = new Catalyst.Adapters.Persistence.YamlConfigRepository();
            var settings = new Catalyst.Adapters.Persistence.JsonSettingsStorage(Path.Combine(tempDir, "settings.json"));
            var pathResolver = new Catalyst.Adapters.Persistence.PathResolver(settings);
            string configPath = Path.Combine(tempDir, "apps.yaml");
            pathResolver.CustomConfigPath = configPath;
            var configService = new Catalyst.Core.Services.AppConfigurationService(configRepo, settings, pathResolver);

            var iconRenderer = new Catalyst.Adapters.Icons.SkiaIconRenderer();
            var iconService = new Catalyst.Core.Services.IconManagementService(iconRenderer, configService);
            var hotkeyHook = new Catalyst.Adapters.Platform.WindowsHotkeyHook();
            var hotkey = new Catalyst.Core.Services.HotkeyService(hotkeyHook);
            var mockWindowService = new MockWindowService(() => { });

            string customTargetDir = Path.Combine(tempDir, "MyCustomPack", ".catalyst");

            var app = new AppInfo
            {
                Name = "Clipper",
                Color = "#FF9800",
                Label = "CL",
                CatalystDirectory = customTargetDir,
                CustomGlyphSvg = "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 16 16\"><path d=\"M1 1h14v14H1z\" fill=\"#fff\"/></svg>"
            };

            configService.SaveApps(new[] { app }, configPath);

            var mgmtVm = new Catalyst.Adapters.UI.ViewModels.AppManagementViewModel(
                configService, iconService, hotkey, mockWindowService);

            string exported = mgmtVm.ExportCatalystFolder(app);
            Assert.Equal(customTargetDir, exported);
            Assert.True(File.Exists(Path.Combine(customTargetDir, "catalystApp.yaml")));
            Assert.False(File.Exists(Path.Combine(customTargetDir, "app.yaml")));
            Assert.False(File.Exists(Path.Combine(customTargetDir, "catalyst.yaml")));
            Assert.True(File.Exists(Path.Combine(customTargetDir, "assets", "custom-glyph.svg")));
            Assert.False(Directory.Exists(Path.Combine(customTargetDir, "icons")));
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    [Fact]
    public void PathsAndDefaults_AreRelativeToProjectFileParentDirectory()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "RelativePathsTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            string projDir = Path.Combine(tempDir, "FlightPlan");
            Directory.CreateDirectory(projDir);
            string csprojPath = Path.Combine(projDir, "FlightPlan.csproj");
            File.WriteAllText(csprojPath, "<Project Sdk=\"Microsoft.NET.Sdk\"/>");

            string iconsDir = Path.Combine(projDir, "icons");
            Directory.CreateDirectory(iconsDir);
            string customGlyphPath = Path.Combine(iconsDir, "custom-glyph.svg");
            File.WriteAllText(customGlyphPath, "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 10 10\"><circle cx=\"5\" cy=\"5\" r=\"5\"/></svg>");
            string svgOverridePath = Path.Combine(iconsDir, "full-icon.svg");
            File.WriteAllText(svgOverridePath, "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 10 10\"><rect width=\"10\" height=\"10\"/></svg>");

            string wwwrootDir = Path.Combine(projDir, "wwwroot");
            Directory.CreateDirectory(wwwrootDir);
            string faviconPath = Path.Combine(wwwrootDir, "favicon.ico");
            File.WriteAllText(faviconPath, "fake-ico");

            string catalystDir = Path.Combine(projDir, ".catalyst");
            Directory.CreateDirectory(catalystDir);

            var configRepo = new Catalyst.Adapters.Persistence.YamlConfigRepository();
            var settings = new Catalyst.Adapters.Persistence.JsonSettingsStorage(Path.Combine(tempDir, "settings.json"));
            var pathResolver = new Catalyst.Adapters.Persistence.PathResolver(settings);
            string configPath = Path.Combine(tempDir, "apps.yaml");
            pathResolver.CustomConfigPath = configPath;
            var configService = new Catalyst.Core.Services.AppConfigurationService(configRepo, settings, pathResolver);

            var app = new AppInfo
            {
                Name = "FlightPlan",
                Color = "#1E88E4",
                Label = "FP",
                ProjectPath = csprojPath,
                CustomGlyphSvg = customGlyphPath,
                SvgOverride = svgOverridePath,
                FaviconPath = faviconPath,
                CatalystDirectory = catalystDir
            };

            var entry = configService.ConvertToConfigEntry(app, tempDir);

            // Verify serialized config entry paths are relative to the project directory
            Assert.Equal(@"icons\custom-glyph.svg", entry.Icon.CustomGlyphSvg.Replace('/', '\\'));
            Assert.Equal(@"icons\full-icon.svg", entry.Icon.SvgOverride.Replace('/', '\\'));
            Assert.Equal(@"wwwroot\favicon.ico", entry.Icon.FaviconPath.Replace('/', '\\'));
            Assert.Equal(".catalyst", entry.CatalystDirectory);

            // Verify converting back to AppInfo resolves them back to full paths in project directory
            var loadedApp = configService.ConvertToAppInfo(entry, tempDir, Path.Combine(tempDir, "global-icons"));
            Assert.Equal(customGlyphPath, loadedApp.CustomGlyphSvg);
            Assert.Equal(svgOverridePath, loadedApp.SvgOverride);
            Assert.Equal(faviconPath, loadedApp.FaviconPath);
            Assert.Equal(catalystDir, loadedApp.CatalystDirectory);
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    [Fact]
    public void FaviconPath_DefaultDiscovery_DiscoversRelativeToProjectDirectory()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "FaviconDefaultTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            string projDir = Path.Combine(tempDir, "Warpdeck");
            Directory.CreateDirectory(projDir);
            string csprojPath = Path.Combine(projDir, "Warpdeck.csproj");
            File.WriteAllText(csprojPath, "<Project Sdk=\"Microsoft.NET.Sdk\"/>");

            string wwwrootDir = Path.Combine(projDir, "wwwroot");
            Directory.CreateDirectory(wwwrootDir);
            string defaultFaviconPath = Path.Combine(wwwrootDir, "favicon.ico");
            File.WriteAllText(defaultFaviconPath, "fake-favicon-content");

            var configRepo = new Catalyst.Adapters.Persistence.YamlConfigRepository();
            var settings = new Catalyst.Adapters.Persistence.JsonSettingsStorage(Path.Combine(tempDir, "settings.json"));
            var pathResolver = new Catalyst.Adapters.Persistence.PathResolver(settings);
            var configService = new Catalyst.Core.Services.AppConfigurationService(configRepo, settings, pathResolver);

            var entry = new AppConfigEntry
            {
                Name = "Warpdeck",
                Launch = new LaunchConfig { ProjectPath = csprojPath },
                Icon = new IconConfig { Color = "#2196F3" }
            };

            var appInfo = configService.ConvertToAppInfo(entry, tempDir, Path.Combine(tempDir, "icons"));

            // Should discover the favicon in project directory
            Assert.Equal(defaultFaviconPath, appInfo.FaviconPath);

            // When converted back to config entry, should be serialized relative to project directory
            var serializedEntry = configService.ConvertToConfigEntry(appInfo, tempDir);
            Assert.Equal(@"wwwroot\favicon.ico", serializedEntry.Icon.FaviconPath.Replace('/', '\\'));
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    [Fact]
    public void ExportCatalystFolder_DefaultsCatalystDirectoryRelativeToProjectParent()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "CatalystDefaultExportTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            string projDir = Path.Combine(tempDir, "MyApp");
            Directory.CreateDirectory(projDir);
            string csprojPath = Path.Combine(projDir, "MyApp.csproj");
            File.WriteAllText(csprojPath, "<Project Sdk=\"Microsoft.NET.Sdk\"/>");

            var configRepo = new Catalyst.Adapters.Persistence.YamlConfigRepository();
            var settings = new Catalyst.Adapters.Persistence.JsonSettingsStorage(Path.Combine(tempDir, "settings.json"));
            var pathResolver = new Catalyst.Adapters.Persistence.PathResolver(settings);
            var configService = new Catalyst.Core.Services.AppConfigurationService(configRepo, settings, pathResolver);
            var iconRenderer = new Catalyst.Adapters.Icons.SkiaIconRenderer();
            var iconService = new Catalyst.Core.Services.IconManagementService(iconRenderer, configService);

            var app = new AppInfo
            {
                Name = "MyApp",
                Color = "#336699",
                Label = "MA",
                ProjectPath = csprojPath,
                CatalystDirectory = "" // Empty default
            };

            string exportedDir = iconService.ExportCatalystFolder(app);

            Assert.Equal(Path.Combine(projDir, ".catalyst"), exportedDir);
            Assert.Equal(".catalyst", app.CatalystDirectory);
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    [Fact]
    public void UpdateProjectFavicon_ResolvesRelativeFaviconPathAgainstProjectParent()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "FaviconUpdateRelativeTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            string projDir = Path.Combine(tempDir, "MyWebProject");
            Directory.CreateDirectory(projDir);
            string csprojPath = Path.Combine(projDir, "MyWebProject.csproj");
            File.WriteAllText(csprojPath, "<Project Sdk=\"Microsoft.NET.Sdk.Web\">\n  <PropertyGroup>\n    <TargetFramework>net9.0</TargetFramework>\n  </PropertyGroup>\n</Project>");

            var configRepo = new Catalyst.Adapters.Persistence.YamlConfigRepository();
            var settings = new Catalyst.Adapters.Persistence.JsonSettingsStorage(Path.Combine(tempDir, "settings.json"));
            var pathResolver = new Catalyst.Adapters.Persistence.PathResolver(settings);
            string configPath = Path.Combine(tempDir, "apps.yaml");
            pathResolver.CustomConfigPath = configPath;
            var configService = new Catalyst.Core.Services.AppConfigurationService(configRepo, settings, pathResolver);

            var iconRenderer = new Catalyst.Adapters.Icons.SkiaIconRenderer();
            var iconService = new Catalyst.Core.Services.IconManagementService(iconRenderer, configService);

            var app = new AppInfo
            {
                Name = "MyWebProject",
                Color = "#4CAF50",
                Label = "WP",
                ProjectPath = csprojPath,
                FaviconPath = @"wwwroot\favicon.ico" // Relative to project parent!
            };

            bool updated = iconService.UpdateProjectFavicon(app);
            Assert.True(updated);

            string expectedFaviconPath = Path.Combine(projDir, "wwwroot", "favicon.ico");
            Assert.True(File.Exists(expectedFaviconPath));
            Assert.True(new FileInfo(expectedFaviconPath).Length > 0);
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    [Fact]
    public void SkiaIconRenderer_LoadsSvgInputsRelativeToProjectParent()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "SkiaRelativeSvgTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            string projDir = Path.Combine(tempDir, "SvgProj");
            Directory.CreateDirectory(projDir);
            string csprojPath = Path.Combine(projDir, "SvgProj.csproj");
            File.WriteAllText(csprojPath, "<Project Sdk=\"Microsoft.NET.Sdk\"/>");

            string iconsDir = Path.Combine(projDir, "assets");
            Directory.CreateDirectory(iconsDir);
            string customGlyphPath = Path.Combine(iconsDir, "glyph.svg");
            File.WriteAllText(customGlyphPath, "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 100 100\"><circle cx=\"50\" cy=\"50\" r=\"40\" fill=\"white\"/></svg>");

            var iconRenderer = new Catalyst.Adapters.Icons.SkiaIconRenderer();

            var app = new AppInfo
            {
                Name = "SvgProj",
                Color = "#222222",
                ProjectPath = csprojPath,
                CustomGlyphSvg = @"assets\glyph.svg" // Relative to project parent
            };

            using var bmp = iconRenderer.RenderIconBitmap(app, 64, Path.Combine(tempDir, "global-icons"), tempDir);
            Assert.NotNull(bmp);
            Assert.Equal(64, bmp.Width);
            Assert.Equal(64, bmp.Height);
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    [Fact]
    public void Paths_DefaultRelativeToLaunchTargetDirectory_WhenLaunchIsExecutable()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "ExecRelativePathsTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            // Create root files to make sure they do not hijack the relative paths
            File.WriteAllText(Path.Combine(tempDir, "favicon.ico"), "root-favicon");
            Directory.CreateDirectory(Path.Combine(tempDir, ".catalyst"));
            Directory.CreateDirectory(Path.Combine(tempDir, "icons"));
            File.WriteAllText(Path.Combine(tempDir, "icons", "glyph.svg"), "<svg>root</svg>");

            string targetDir = Path.Combine(tempDir, "MyExeApp");
            Directory.CreateDirectory(targetDir);
            string exePath = Path.Combine(targetDir, "MyExeApp.exe");
            File.WriteAllText(exePath, "fake-exe");

            var configRepo = new Catalyst.Adapters.Persistence.YamlConfigRepository();
            var settings = new Catalyst.Adapters.Persistence.JsonSettingsStorage(Path.Combine(tempDir, "settings.json"));
            var pathResolver = new Catalyst.Adapters.Persistence.PathResolver(settings);
            string configPath = Path.Combine(tempDir, "apps.yaml");
            pathResolver.CustomConfigPath = configPath;
            var configService = new Catalyst.Core.Services.AppConfigurationService(configRepo, settings, pathResolver);

            var entry = new AppConfigEntry
            {
                Name = "MyExeApp",
                Launch = new LaunchConfig
                {
                    ExecutablePath = exePath
                },
                Icon = new IconConfig
                {
                    FaviconPath = "favicon.ico",
                    CustomGlyphSvg = @"icons\glyph.svg",
                    SvgOverride = @"icons\override.svg"
                },
                CatalystDirectory = ".catalyst"
            };

            var appInfo = configService.ConvertToAppInfo(entry, tempDir, Path.Combine(tempDir, "global-icons"));

            // All relative paths must be resolved relative to targetDir (launch target directory), not tempDir (apps.yaml)
            Assert.Equal(Path.Combine(targetDir, "favicon.ico"), appInfo.FaviconPath);
            Assert.Equal(Path.Combine(targetDir, ".catalyst"), appInfo.CatalystDirectory);
            Assert.Equal(Path.Combine(targetDir, "icons", "glyph.svg"), appInfo.CustomGlyphSvg);
            Assert.Equal(Path.Combine(targetDir, "icons", "override.svg"), appInfo.SvgOverride);

            // Converting back to config entry should serialize them relative to targetDir
            var reserialized = configService.ConvertToConfigEntry(appInfo, tempDir);
            Assert.Equal("favicon.ico", reserialized.Icon.FaviconPath.Replace('/', '\\'));
            Assert.Equal(".catalyst", reserialized.CatalystDirectory);
            Assert.Equal(@"icons\glyph.svg", reserialized.Icon.CustomGlyphSvg.Replace('/', '\\'));
            Assert.Equal(@"icons\override.svg", reserialized.Icon.SvgOverride.Replace('/', '\\'));
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    [Fact]
    public void Paths_FallbackToRootDir_WhenNoLaunchTargetSpecified()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "NoTargetPathsTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var configRepo = new Catalyst.Adapters.Persistence.YamlConfigRepository();
            var settings = new Catalyst.Adapters.Persistence.JsonSettingsStorage(Path.Combine(tempDir, "settings.json"));
            var pathResolver = new Catalyst.Adapters.Persistence.PathResolver(settings);
            var configService = new Catalyst.Core.Services.AppConfigurationService(configRepo, settings, pathResolver);

            var entry = new AppConfigEntry
            {
                Name = "NoTargetApp",
                Icon = new IconConfig
                {
                    FaviconPath = "favicon.ico",
                    CustomGlyphSvg = @"icons\glyph.svg",
                    SvgOverride = @"icons\override.svg"
                },
                CatalystDirectory = ".catalyst"
            };

            var appInfo = configService.ConvertToAppInfo(entry, tempDir, Path.Combine(tempDir, "global-icons"));

            // When no launch target is configured, relative paths fall back to rootDir (apps.yaml directory)
            Assert.Equal(Path.Combine(tempDir, "favicon.ico"), appInfo.FaviconPath);
            Assert.Equal(Path.Combine(tempDir, ".catalyst"), appInfo.CatalystDirectory);
            Assert.Equal(Path.Combine(tempDir, "icons", "glyph.svg"), appInfo.CustomGlyphSvg);
            Assert.Equal(Path.Combine(tempDir, "icons", "override.svg"), appInfo.SvgOverride);
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    [Fact]
    public void SkiaIconRenderer_LoadsSvgRelativeToExecutableDirectory_WhenOnlyExecutableIsSet()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "SkiaExeSvgTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            string appDir = Path.Combine(tempDir, "ExeAppDir");
            Directory.CreateDirectory(appDir);
            string exePath = Path.Combine(appDir, "MyApp.exe");
            File.WriteAllText(exePath, "dummy-binary");

            string iconsDir = Path.Combine(appDir, "icons");
            Directory.CreateDirectory(iconsDir);
            string customGlyphPath = Path.Combine(iconsDir, "glyph.svg");
            File.WriteAllText(customGlyphPath, "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 100 100\"><circle cx=\"50\" cy=\"50\" r=\"40\" fill=\"white\"/></svg>");

            var iconRenderer = new Catalyst.Adapters.Icons.SkiaIconRenderer();

            var app = new AppInfo
            {
                Name = "ExeApp",
                Color = "#222222",
                ExecutablePath = exePath,
                CustomGlyphSvg = @"icons\glyph.svg" // Relative to exe target directory
            };

            using var bmp = iconRenderer.RenderIconBitmap(app, 64, Path.Combine(tempDir, "global-icons"), tempDir);
            Assert.NotNull(bmp);
            Assert.Equal(64, bmp.Width);
            Assert.Equal(64, bmp.Height);
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    [Fact]
    public void Paths_DefaultRelativeToLaunchTargetDirectory_WhenLaunchIsWorkingDirectory()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "WorkDirRelativePathsTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            string workDir = Path.Combine(tempDir, "TargetWorkDir");
            Directory.CreateDirectory(workDir);

            var configRepo = new Catalyst.Adapters.Persistence.YamlConfigRepository();
            var settings = new Catalyst.Adapters.Persistence.JsonSettingsStorage(Path.Combine(tempDir, "settings.json"));
            var pathResolver = new Catalyst.Adapters.Persistence.PathResolver(settings);
            var configService = new Catalyst.Core.Services.AppConfigurationService(configRepo, settings, pathResolver);

            var entry = new AppConfigEntry
            {
                Name = "WorkDirApp",
                Launch = new LaunchConfig
                {
                    WorkingDirectory = workDir
                },
                Icon = new IconConfig
                {
                    FaviconPath = "favicon.ico",
                    CustomGlyphSvg = @"assets\glyph.svg"
                },
                CatalystDirectory = ".catalyst"
            };

            var appInfo = configService.ConvertToAppInfo(entry, tempDir, Path.Combine(tempDir, "global-icons"));

            Assert.Equal(Path.Combine(workDir, "favicon.ico"), appInfo.FaviconPath);
            Assert.Equal(Path.Combine(workDir, ".catalyst"), appInfo.CatalystDirectory);
            Assert.Equal(Path.Combine(workDir, "assets", "glyph.svg"), appInfo.CustomGlyphSvg);
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    [Fact]
    public void AppConfigurationService_ResolveExpectedFullPath_CorrectlyCalculatesPaths()
    {
        string rootDir = @"C:\Catalyst";
        string projectDir = @"C:\Projects\FlightPlan";

        // 1. Relative path with projectDir (relative to launch target)
        string resolvedProjectRelative = Catalyst.Core.Services.AppConfigurationService.ResolveExpectedFullPath(@"icons\glyph.svg", projectDir, rootDir);
        Assert.Equal(Path.GetFullPath(Path.Combine(projectDir, @"icons\glyph.svg")), resolvedProjectRelative);

        // 2. Relative path without projectDir (falls back to rootDir)
        string resolvedRootRelative = Catalyst.Core.Services.AppConfigurationService.ResolveExpectedFullPath(@"icons\glyph.svg", null, rootDir);
        Assert.Equal(Path.GetFullPath(Path.Combine(rootDir, @"icons\glyph.svg")), resolvedRootRelative);

        // 3. Full absolute path remains unchanged
        string absolutePath = @"D:\Assets\Icons\app.svg";
        string resolvedAbsolute = Catalyst.Core.Services.AppConfigurationService.ResolveExpectedFullPath(absolutePath, projectDir, rootDir);
        Assert.Equal(Path.GetFullPath(absolutePath), resolvedAbsolute);

        // 4. Default fallback when empty string provided
        string resolvedCatDefault = Catalyst.Core.Services.AppConfigurationService.ResolveExpectedFullPath("", projectDir, rootDir, ".catalyst");
        Assert.Equal(Path.GetFullPath(Path.Combine(projectDir, ".catalyst")), resolvedCatDefault);

        string resolvedFavDefault = Catalyst.Core.Services.AppConfigurationService.ResolveExpectedFullPath(null, projectDir, rootDir, "favicon.ico");
        Assert.Equal(Path.GetFullPath(Path.Combine(projectDir, "favicon.ico")), resolvedFavDefault);

        // 5. Empty path without default fallback returns empty string
        string resolvedEmpty = Catalyst.Core.Services.AppConfigurationService.ResolveExpectedFullPath("", projectDir, rootDir);
        Assert.Equal(string.Empty, resolvedEmpty);

        // 6. Inline SVG XML is preserved
        string inlineSvg = "<svg><circle cx=\"10\" cy=\"10\" r=\"5\"/></svg>";
        string resolvedInline = Catalyst.Core.Services.AppConfigurationService.ResolveExpectedFullPath(inlineSvg, projectDir, rootDir);
        Assert.Equal(inlineSvg, resolvedInline);

        // 7. URLs are preserved
        string url = "https://github.com/armunro/catalyst";
        string resolvedUrl = Catalyst.Core.Services.AppConfigurationService.ResolveExpectedFullPath(url, projectDir, rootDir);
        Assert.Equal(url, resolvedUrl);
    }

    [Fact]
    public void SingleInstanceManager_FirstInstanceAcquires_SecondInstanceFailsAndSignalsFirstInstance()
    {
        string uniqueId = Guid.NewGuid().ToString("N");
        string mutexName = $"Test_Mutex_{uniqueId}";
        string eventName = $"Test_Event_{uniqueId}";

        var signalReceived = new System.Threading.ManualResetEventSlim(false);

        using var firstManager = new Catalyst.Adapters.Platform.SingleInstanceManager(mutexName, eventName);
        bool firstAcquired = firstManager.TryAcquireSingleInstance(() =>
        {
            signalReceived.Set();
        });

        Assert.True(firstAcquired);

        using var secondManager = new Catalyst.Adapters.Platform.SingleInstanceManager(mutexName, eventName);
        bool secondAcquired = secondManager.TryAcquireSingleInstance(() => { });

        Assert.False(secondAcquired);

        secondManager.NotifyExistingInstance();

        bool signalHandled = signalReceived.Wait(TimeSpan.FromSeconds(5));
        Assert.True(signalHandled);
    }

    [Fact]
    public void SingleInstanceManager_DisposedFirstInstance_AllowsSubsequentInstanceToAcquire()
    {
        string uniqueId = Guid.NewGuid().ToString("N");
        string mutexName = $"Test_Mutex_{uniqueId}";
        string eventName = $"Test_Event_{uniqueId}";

        var firstManager = new Catalyst.Adapters.Platform.SingleInstanceManager(mutexName, eventName);
        bool firstAcquired = firstManager.TryAcquireSingleInstance(() => { });
        Assert.True(firstAcquired);

        firstManager.Dispose();

        using var secondManager = new Catalyst.Adapters.Platform.SingleInstanceManager(mutexName, eventName);
        bool secondAcquired = secondManager.TryAcquireSingleInstance(() => { });
        Assert.True(secondAcquired);
    }

    [Fact]
    public void WebUrlApp_PropertiesAndMonitoring_AreDisabled()
    {
        var app = new AppInfo
        {
            Name = "Web Dashboard",
            ExecutablePath = "http://localhost:3000/dashboard"
        };

        Assert.True(app.IsUrl);
        Assert.False(app.CanMonitorProcess);
        Assert.False(app.CanViewLogs);
        Assert.False(app.IsRunning);

        // Even if Process property were set, IsRunning should stay false for URLs
        app.Process = System.Diagnostics.Process.GetCurrentProcess();
        Assert.False(app.IsRunning);
    }

    [Fact]
    public void AppLauncherService_DetectRunningApps_SkipsWebUrls()
    {
        var procExec = new Catalyst.Adapters.Processes.WindowsProcessExecutor();
        var configRepo = new Catalyst.Adapters.Persistence.YamlConfigRepository();
        var settings = new Catalyst.Adapters.Persistence.JsonSettingsStorage();
        var resolver = new Catalyst.Adapters.Persistence.PathResolver(settings);
        var configService = new Catalyst.Core.Services.AppConfigurationService(configRepo, settings, resolver);
        var launcher = new Catalyst.Core.Services.AppLauncherService(procExec, configService);

        var currentProcessName = System.Diagnostics.Process.GetCurrentProcess().ProcessName;
        var urlApp = new AppInfo
        {
            Name = "UrlAppMatchingProcessName",
            ExecutablePath = $"https://example.com/{currentProcessName}"
        };

        launcher.DetectRunningApps(new[] { urlApp });
        Assert.Null(urlApp.Process);
        Assert.False(urlApp.IsRunning);
    }

    [Fact]
    public void MainWindowViewModel_OpenLogViewer_DoesNotShowLogs_ForWebUrlApps()
    {
        bool logViewerShown = false;
        var mockWindowService = new MockLogWindowService(() => logViewerShown = true);

        var procExec = new Catalyst.Adapters.Processes.WindowsProcessExecutor();
        var configRepo = new Catalyst.Adapters.Persistence.YamlConfigRepository();
        var settings = new Catalyst.Adapters.Persistence.JsonSettingsStorage();
        var resolver = new Catalyst.Adapters.Persistence.PathResolver(settings);
        var configService = new Catalyst.Core.Services.AppConfigurationService(configRepo, settings, resolver);
        var launcher = new Catalyst.Core.Services.AppLauncherService(procExec, configService);
        var hotkeyHook = new Catalyst.Adapters.Platform.WindowsHotkeyHook();
        var hotkeyService = new Catalyst.Core.Services.HotkeyService(hotkeyHook);

        var vm = new Catalyst.Adapters.UI.ViewModels.MainWindowViewModel(configService, launcher, hotkeyService, mockWindowService);

        var urlApp = new AppInfo
        {
            Name = "Web App",
            ExecutablePath = "https://example.com"
        };

        vm.OpenLogViewer(urlApp);
        Assert.False(logViewerShown);

        var regularApp = new AppInfo
        {
            Name = "CLI App",
            ExecutablePath = "app.exe"
        };

        vm.OpenLogViewer(regularApp);
        Assert.True(logViewerShown);
    }

    [Fact]
    public void BooleanToHiddenVisibilityConverter_ConvertsCorrectly()
    {
        var converter = new Catalyst.BooleanToHiddenVisibilityConverter();

        Assert.Equal(System.Windows.Visibility.Visible, converter.Convert(true, typeof(System.Windows.Visibility), null!, System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal(System.Windows.Visibility.Hidden, converter.Convert(false, typeof(System.Windows.Visibility), null!, System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal(System.Windows.Visibility.Hidden, converter.Convert("invalid", typeof(System.Windows.Visibility), null!, System.Globalization.CultureInfo.InvariantCulture));

        Assert.True((bool)converter.ConvertBack(System.Windows.Visibility.Visible, typeof(bool), null!, System.Globalization.CultureInfo.InvariantCulture));
        Assert.False((bool)converter.ConvertBack(System.Windows.Visibility.Hidden, typeof(bool), null!, System.Globalization.CultureInfo.InvariantCulture));
        Assert.False((bool)converter.ConvertBack(System.Windows.Visibility.Collapsed, typeof(bool), null!, System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public void SharedTheme_ContainsForegroundBrush_ResolvingToForegroundColor()
    {
        var dict = new System.Windows.ResourceDictionary
        {
            Source = new Uri("pack://application:,,,/Catalyst;component/Themes/SharedTheme.xaml", UriKind.Absolute)
        };

        Assert.True(dict.Contains("ForegroundColor"));
        Assert.True(dict.Contains("ForegroundBrush"));
        var brush = dict["ForegroundBrush"] as SolidColorBrush;
        Assert.NotNull(brush);
        Assert.Equal(dict["ForegroundColor"], brush.Color);
    }

    private class MockLogWindowService : Catalyst.Adapters.UI.Navigation.IWindowService
    {
        private readonly Action _onShowLogViewer;

        public MockLogWindowService(Action onShowLogViewer)
        {
            _onShowLogViewer = onShowLogViewer;
        }

        public void ShowMainWindow() { }
        public void ShowAppManagement(System.Windows.Window? owner = null) { }
        public void ShowSettings(System.Windows.Window? owner = null) { }
        public void ShowLogViewer(AppInfo app, System.Windows.Window? owner = null) => _onShowLogViewer();
        public void ShowIconsResult(string iconsPath, List<(string AppName, string IconPath)> generatedIcons, System.Windows.Window? owner = null) { }
        public void PositionBottomRight(System.Windows.Window window) { }
    }

    private class MockWindowService : Catalyst.Adapters.UI.Navigation.IWindowService
    {
        private readonly Action _onShowSettings;

        public MockWindowService(Action onShowSettings)
        {
            _onShowSettings = onShowSettings;
        }

        public void ShowMainWindow() { }
        public void ShowAppManagement(System.Windows.Window? owner = null) { }
        public void ShowSettings(System.Windows.Window? owner = null) => _onShowSettings();
        public void ShowLogViewer(AppInfo app, System.Windows.Window? owner = null) { }
        public void ShowIconsResult(string iconsPath, List<(string AppName, string IconPath)> generatedIcons, System.Windows.Window? owner = null) { }
        public void PositionBottomRight(System.Windows.Window window) { }
    }
}
