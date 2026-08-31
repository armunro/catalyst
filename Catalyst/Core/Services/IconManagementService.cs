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
                
                using (var stream = File.Open(faviconPngPath, FileMode.Create, FileAccess.Write))
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
}
