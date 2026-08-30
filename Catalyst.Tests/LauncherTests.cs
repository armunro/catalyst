using System;
using System.IO;
using System.Windows.Media;
using Catalyst;
using Catalyst.Services;
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
}
