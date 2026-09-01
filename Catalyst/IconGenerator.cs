using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Media.Imaging;
using Catalyst.Adapters.Icons;
using Catalyst.Core.Ports.Inbound;
using Catalyst.Core.Ports.Outbound;
using Catalyst.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using SkiaSharp;

namespace Catalyst;

/// <summary>
/// Backward-compatible facade delegating to hexagonal icon renderer and management service.
/// </summary>
public class IconGenerator
{
    private static IIconRenderer GetRenderer() =>
        App.Services?.GetService<IIconRenderer>() ?? new SkiaIconRenderer();

    private static IIconManagementService GetManagementService() =>
        App.Services?.GetService<IIconManagementService>() ?? new IconManagementService(
            GetRenderer(),
            new AppConfigurationService(
                new Adapters.Persistence.YamlConfigRepository(),
                new Adapters.Persistence.JsonSettingsStorage(),
                new Adapters.Persistence.PathResolver(new Adapters.Persistence.JsonSettingsStorage())));

    public void Generate(List<AppInfo> apps, string rootDir, Action<string>? logger = null)
    {
        string iconsBaseDir = Path.Combine(rootDir, "icons");
        if (!Directory.Exists(iconsBaseDir))
        {
            Directory.CreateDirectory(iconsBaseDir);
        }

        foreach (var app in apps)
        {
            GenerateIcon(app, iconsBaseDir, logger, rootDir);
        }
    }

    public void GenerateIcon(AppInfo app, string iconsBaseDir, Action<string>? logger = null, string? rootDir = null)
    {
        logger?.Invoke($"Generating icon for {app.Name}...");
        string appDir = Path.Combine(iconsBaseDir, app.Name);
        if (!Directory.Exists(appDir))
        {
            Directory.CreateDirectory(appDir);
        }

        var renderer = GetRenderer();
        using var mainBitmap = renderer.RenderIconBitmap(app, 512, iconsBaseDir, rootDir, logger);

        // Save main icon (512x512)
        string mainOutputPath = Path.Combine(appDir, $"{app.Name}.png");
        try
        {
            renderer.SavePng(mainBitmap, mainOutputPath);
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
            renderer.SaveSvg(app, svgOutputPath, 512, iconsBaseDir, rootDir, logger);
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
            using var sizeBitmap = renderer.RenderIconBitmap(app, fSize, iconsBaseDir, rootDir, logger);
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
                renderer.SaveAsIco(iconImages, iconDimensions, icoPath);
                logger?.Invoke($"Generated favicon.ico for {app.Name}");
            }
        }
        catch (Exception ex)
        {
            logger?.Invoke($"FAILED to save favicon.ico for {app.Name}: {ex.Message}");
        }
    }

    public BitmapSource? RenderPreview(AppInfo app, string iconsBaseDir, string? rootDir = null, int size = 512)
    {
        return GetRenderer().RenderPreview(app, iconsBaseDir, rootDir, size);
    }

    public SKBitmap RenderIconBitmap(AppInfo app, int size, string iconsBaseDir, string? rootDir = null, Action<string>? logger = null)
    {
        return GetRenderer().RenderIconBitmap(app, size, iconsBaseDir, rootDir, logger);
    }

    public string RenderIconSvg(AppInfo app, int size, string iconsBaseDir, string? rootDir = null, Action<string>? logger = null)
    {
        return GetRenderer().RenderIconSvg(app, size, iconsBaseDir, rootDir, logger);
    }

    public void SaveSvg(AppInfo app, string outputPath, int size, string iconsBaseDir, string? rootDir = null, Action<string>? logger = null)
    {
        GetRenderer().SaveSvg(app, outputPath, size, iconsBaseDir, rootDir, logger);
    }

    public static BitmapSource ToBitmapSource(SKBitmap bitmap)
    {
        return SkiaIconRenderer.ToBitmapSource(bitmap);
    }
}
