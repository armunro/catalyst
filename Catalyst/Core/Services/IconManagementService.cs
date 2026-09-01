using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Media.Imaging;
using Catalyst.Core.Ports.Inbound;
using Catalyst.Core.Ports.Outbound;
using SkiaSharp;

namespace Catalyst.Core.Services;

public class IconManagementService : IIconManagementService
{
    private readonly IIconRenderer _iconRenderer;
    private readonly IAppConfigurationService _configurationService;

    public IconManagementService(
        IIconRenderer iconRenderer,
        IAppConfigurationService configurationService)
    {
        _iconRenderer = iconRenderer;
        _configurationService = configurationService;
    }

    public void GenerateAllIcons(IEnumerable<AppInfo> apps, Action<string>? logger = null)
    {
        string iconsBaseDir = _configurationService.IconsBaseDir;
        if (!Directory.Exists(iconsBaseDir))
        {
            Directory.CreateDirectory(iconsBaseDir);
        }

        foreach (var app in apps)
        {
            GenerateIcon(app, logger);
        }
    }

    public void GenerateIcon(AppInfo app, Action<string>? logger = null)
    {
        string iconsBaseDir = _configurationService.IconsBaseDir;
        string rootDir = _configurationService.RootDir;

        logger?.Invoke($"Generating icon for {app.Name}...");
        string appDir = Path.Combine(iconsBaseDir, app.Name);
        if (!Directory.Exists(appDir))
        {
            Directory.CreateDirectory(appDir);
        }

        using var mainBitmap = _iconRenderer.RenderIconBitmap(app, 512, iconsBaseDir, rootDir, logger);

        // Save main icon (512x512)
        string mainOutputPath = Path.Combine(appDir, $"{app.Name}.png");
        try
        {
            _iconRenderer.SavePng(mainBitmap, mainOutputPath);
            logger?.Invoke($"Generated main icon for {app.Name} at {mainOutputPath}");
        }
        catch (Exception ex)
        {
            logger?.Invoke($"FAILED to save main icon for {app.Name}: {ex.Message}");
        }

        // Save SVG icon
        string svgOutputPath = Path.Combine(appDir, $"{app.Name}.svg");
        try
        {
            _iconRenderer.SaveSvg(app, svgOutputPath, 512, iconsBaseDir, rootDir, logger);
            logger?.Invoke($"Generated SVG icon for {app.Name} at {svgOutputPath}");
        }
        catch (Exception ex)
        {
            logger?.Invoke($"FAILED to save SVG icon for {app.Name}: {ex.Message}");
        }

        // Generate Favicons and ICO rendered directly at each target resolution
        int[] faviconSizes = { 16, 32, 48 };
        var iconImages = new List<byte[]>();
        var iconDimensions = new List<(int Width, int Height)>();

        foreach (var fSize in faviconSizes)
        {
            using var sizeBitmap = _iconRenderer.RenderIconBitmap(app, fSize, iconsBaseDir, rootDir, logger);
            if (sizeBitmap == null) continue;

            string faviconPngPath = Path.Combine(appDir, $"favicon-{fSize}x{fSize}.png");
            try
            {
                using var image = SKImage.FromBitmap(sizeBitmap);
                using var data = image.Encode(SKEncodedImageFormat.Png, 100);
                
                using (var stream = new FileStream(faviconPngPath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite))
                {
                    data.SaveTo(stream);
                }

                byte[] bytes = data.ToArray();
                iconImages.Add(bytes);
                iconDimensions.Add((fSize, fSize));
            }
            catch (Exception ex)
            {
                logger?.Invoke($"FAILED to save favicon {fSize}x{fSize} for {app.Name}: {ex.Message}");
            }
        }

        // Save as ICO
        string icoPath = Path.Combine(appDir, "favicon.ico");
        try
        {
            if (iconImages.Count > 0)
            {
                _iconRenderer.SaveAsIco(iconImages, iconDimensions, icoPath);
                logger?.Invoke($"Generated favicon.ico for {app.Name}");
            }
        }
        catch (Exception ex)
        {
            logger?.Invoke($"FAILED to save favicon.ico for {app.Name}: {ex.Message}");
        }
    }

    public BitmapSource? RenderPreview(AppInfo app, int size = 512)
    {
        string iconsBaseDir = _configurationService.IconsBaseDir;
        string rootDir = _configurationService.RootDir;
        return _iconRenderer.RenderPreview(app, iconsBaseDir, rootDir, size);
    }

    public bool UpdateProjectFavicon(AppInfo app, Action<string>? logger = null)
    {
        if (string.IsNullOrWhiteSpace(app.FaviconPath))
        {
            logger?.Invoke($"No favicon path defined for {app.Name}");
            return false;
        }

        string rootDir = _configurationService.RootDir;
        string iconsBaseDir = _configurationService.IconsBaseDir;

        string fullFaviconPath = Path.IsPathRooted(app.FaviconPath)
            ? app.FaviconPath
            : Path.GetFullPath(Path.Combine(rootDir, app.FaviconPath));

        string generatedIcoPath = Path.Combine(iconsBaseDir, app.Name, "favicon.ico");
        string generatedPngPath = Path.Combine(iconsBaseDir, app.Name, "favicon-32x32.png");
        string generatedSvgPath = Path.Combine(iconsBaseDir, app.Name, $"{app.Name}.svg");

        if (!File.Exists(generatedIcoPath) && !File.Exists(generatedPngPath) && !File.Exists(generatedSvgPath))
        {
            GenerateIcon(app, logger);
        }

        bool updated = false;
        if (File.Exists(generatedIcoPath) && fullFaviconPath.EndsWith(".ico", StringComparison.OrdinalIgnoreCase))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(fullFaviconPath)!);
            File.Copy(generatedIcoPath, fullFaviconPath, true);
            updated = true;

            if (!string.IsNullOrEmpty(app.ProjectPath) && app.ProjectPath.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
            {
                UpdateCsprojApplicationIcon(app.ProjectPath, fullFaviconPath, rootDir, logger);
            }
        }
        else if (File.Exists(generatedPngPath) && fullFaviconPath.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(fullFaviconPath)!);
            File.Copy(generatedPngPath, fullFaviconPath, true);
            updated = true;
        }
        else if (File.Exists(generatedSvgPath) && fullFaviconPath.EndsWith(".svg", StringComparison.OrdinalIgnoreCase))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(fullFaviconPath)!);
            File.Copy(generatedSvgPath, fullFaviconPath, true);
            updated = true;
        }

        if (updated)
        {
            logger?.Invoke($"Updated favicon for {app.Name} at {fullFaviconPath}");
        }
        else
        {
            logger?.Invoke($"No matching generated icon found to update favicon for {app.Name}");
        }

        return updated;
    }

    private static void UpdateCsprojApplicationIcon(string projectPath, string iconPath, string rootDir, Action<string>? logger = null)
    {
        try
        {
            string fullProjectPath = Path.IsPathRooted(projectPath) ? projectPath : Path.GetFullPath(Path.Combine(rootDir, projectPath));
            if (!File.Exists(fullProjectPath)) return;

            string projectDir = Path.GetDirectoryName(fullProjectPath)!;
            string relativeIconPath = Path.GetRelativePath(projectDir, iconPath);

            string content = File.ReadAllText(fullProjectPath);
            bool modified = false;

            if (content.Contains("<ApplicationIcon>"))
            {
                var regex = new System.Text.RegularExpressions.Regex(@"<ApplicationIcon>.*?</ApplicationIcon>");
                string newContent = regex.Replace(content, $"<ApplicationIcon>{relativeIconPath}</ApplicationIcon>");
                if (newContent != content)
                {
                    content = newContent;
                    modified = true;
                }
            }
            else
            {
                int index = content.IndexOf("</PropertyGroup>");
                if (index > 0)
                {
                    content = content.Insert(index, $"    <ApplicationIcon>{relativeIconPath}</ApplicationIcon>\n    ");
                    modified = true;
                }
            }

            if (modified)
            {
                File.WriteAllText(fullProjectPath, content);
                logger?.Invoke($"Updated <ApplicationIcon> in {projectPath}");
            }
        }
        catch (Exception ex)
        {
            logger?.Invoke($"Failed to update csproj {projectPath}: {ex.Message}");
        }
    }
}
