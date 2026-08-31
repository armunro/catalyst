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

            Assert.True(File.Exists(mainPng));
            Assert.True(File.Exists(ico));
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
}
