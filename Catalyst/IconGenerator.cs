using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Windows.Media.Imaging;
using SkiaSharp;
using Svg.Skia;

namespace Catalyst;

public class IconGenerator
{
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

        using var mainBitmap = RenderIconBitmap(app, 512, iconsBaseDir, rootDir, logger);

        // Save main icon (512x512)
        string mainOutputPath = Path.Combine(appDir, $"{app.Name}.png");
        try
        {
            using var image = SKImage.FromBitmap(mainBitmap);
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            using var stream = File.Open(mainOutputPath, FileMode.Create, FileAccess.Write);
            data.SaveTo(stream);
            logger?.Invoke($"Generated main icon for {app.Name} at {mainOutputPath}");
        }
        catch (Exception ex)
        {
            logger?.Invoke($"FAILED to save main icon for {app.Name}: {ex.Message}");
        }

        // Generate Favicons and ICO rendered directly at each target resolution for crisp vector edges
        int[] faviconSizes = { 16, 32, 48 };
        var iconImages = new List<byte[]>();
        var iconDimensions = new List<(int Width, int Height)>();

        foreach (var fSize in faviconSizes)
        {
            using var sizeBitmap = RenderIconBitmap(app, fSize, iconsBaseDir, rootDir, logger);
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
                SaveAsIco(iconImages, iconDimensions, icoPath);
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
        try
        {
            using var bitmap = RenderIconBitmap(app, size, iconsBaseDir, rootDir);
            return ToBitmapSource(bitmap);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error generating preview: {ex.Message}");
            return null;
        }
    }

    public SKBitmap RenderIconBitmap(AppInfo app, int size, string iconsBaseDir, string? rootDir = null, Action<string>? logger = null)
    {
        // 2x supersampling for smooth anti-aliased geometry, curves and gradients
        int superScale = 2;
        int renderSize = size * superScale;

        using var highResBitmap = new SKBitmap(renderSize, renderSize, SKColorType.Rgba8888, SKAlphaType.Premul);
        using (var canvas = new SKCanvas(highResBitmap))
        {
            canvas.Clear(SKColors.Transparent);
            RenderToCanvas(canvas, app, renderSize, iconsBaseDir, rootDir, logger);
        }

        var scaled = highResBitmap.Resize(new SKImageInfo(size, size), SKFilterQuality.High);
        if (scaled != null)
        {
            return scaled;
        }

        // Direct fallback
        var directBitmap = new SKBitmap(size, size, SKColorType.Rgba8888, SKAlphaType.Premul);
        using (var directCanvas = new SKCanvas(directBitmap))
        {
            directCanvas.Clear(SKColors.Transparent);
            RenderToCanvas(directCanvas, app, size, iconsBaseDir, rootDir, logger);
        }
        return directBitmap;
    }

    private void RenderToCanvas(SKCanvas canvas, AppInfo app, int size, string iconsBaseDir, string? rootDir = null, Action<string>? logger = null)
    {
        // CASE 1: Complete SVG Override
        if (!string.IsNullOrWhiteSpace(app.SvgOverride))
        {
            try
            {
                var svg = LoadSvgContent(app.SvgOverride, rootDir);
                if (svg?.Picture != null)
                {
                    // Complete SVG overrides preserve full native vector artwork and colors without glyph tint filters
                    DrawScaledPicture(canvas, svg.Picture, 0, 0, size, size, null);
                    return;
                }
            }
            catch (Exception ex)
            {
                logger?.Invoke($"Error rendering SVG override for {app.Name}: {ex.Message}");
            }
        }

        // CASE 2: Background Tile + Glyph/Label
        DrawBackground(canvas, app.Color, app.SecondaryColor, app.BackgroundType, app.GradientDirection, size);

        // 2a. Custom Glyph SVG
        if (!string.IsNullOrWhiteSpace(app.CustomGlyphSvg))
        {
            try
            {
                var svg = LoadSvgContent(app.CustomGlyphSvg, rootDir);
                if (svg?.Picture != null)
                {
                    SKColor? glyphColor = null;
                    if (!string.IsNullOrWhiteSpace(app.CustomGlyphColor) && SKColor.TryParse(app.CustomGlyphColor, out var parsedGlyphColor))
                    {
                        glyphColor = parsedGlyphColor;
                    }

                    float padding = size * 0.16f;
                    float glyphSize = size - (padding * 2);
                    DrawScaledPicture(canvas, svg.Picture, padding, padding, glyphSize, glyphSize, glyphColor);
                    return;
                }
            }
            catch (Exception ex)
            {
                logger?.Invoke($"Error rendering custom glyph SVG for {app.Name}: {ex.Message}");
            }
        }

        // 2b. Bootstrap Icon
        if (!string.IsNullOrWhiteSpace(app.BootstrapIcon))
        {
            try
            {
                string iconName = app.BootstrapIcon.Trim();
                if (iconName.EndsWith(".svg", StringComparison.OrdinalIgnoreCase))
                {
                    iconName = iconName.Substring(0, iconName.Length - 4);
                }

                string svgContent = FetchBootstrapIconSvg(iconName, iconsBaseDir);
                if (!string.IsNullOrEmpty(svgContent))
                {
                    var svg = new SKSvg();
                    svg.FromSvg(svgContent);
                    if (svg.Picture != null)
                    {
                        SKColor bootstrapColor = SKColors.White;
                        if (!string.IsNullOrWhiteSpace(app.CustomGlyphColor) && SKColor.TryParse(app.CustomGlyphColor, out var parsedBootstrapColor))
                        {
                            bootstrapColor = parsedBootstrapColor;
                        }

                        float padding = size * 0.16f;
                        float glyphSize = size - (padding * 2);
                        DrawScaledPicture(canvas, svg.Picture, padding, padding, glyphSize, glyphSize, bootstrapColor);
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                logger?.Invoke($"Error rendering Bootstrap icon for {app.Name}: {ex.Message}");
            }
        }

        // 2c. Fallback Text Label
        if (!string.IsNullOrWhiteSpace(app.Label))
        {
            try
            {
                DrawLabelText(canvas, app.Label, size);
            }
            catch (Exception ex)
            {
                logger?.Invoke($"Error rendering label text for {app.Name}: {ex.Message}");
            }
        }
    }

    public void DrawBackground(SKCanvas canvas, string? colorHex, string? secondaryColorHex, string? backgroundType, string? gradientDirection, int size)
    {
        SKColor primaryColor = SKColor.TryParse(colorHex, out var parsed1) ? parsed1 : SKColor.Parse("#1E88E4");

        bool isGradient = string.Equals(backgroundType, "Gradient", StringComparison.OrdinalIgnoreCase) ||
                          (!string.IsNullOrWhiteSpace(secondaryColorHex) && !string.Equals(backgroundType, "Solid", StringComparison.OrdinalIgnoreCase));

        using var bgPaint = new SKPaint
        {
            IsAntialias = true,
            FilterQuality = SKFilterQuality.High,
            IsDither = true,
            Style = SKPaintStyle.Fill
        };

        float cornerRadius = size * 0.2f;
        var roundRect = new SKRoundRect(new SKRect(0, 0, size, size), cornerRadius, cornerRadius);

        if (isGradient)
        {
            SKColor startColor = primaryColor;
            SKColor endColor;

            if (!string.IsNullOrWhiteSpace(secondaryColorHex) && SKColor.TryParse(secondaryColorHex, out var parsed2))
            {
                endColor = parsed2;
            }
            else
            {
                startColor = Lerp(primaryColor, SKColors.White, 0.35f);
                endColor = Lerp(primaryColor, SKColors.Black, 0.25f);
            }

            SKPoint p0, p1;
            string dir = gradientDirection?.Trim().ToLowerInvariant() ?? "diagonal";
            switch (dir)
            {
                case "vertical":
                case "topbottom":
                case "toptobottom":
                    p0 = new SKPoint(size / 2f, 0);
                    p1 = new SKPoint(size / 2f, size);
                    break;
                case "horizontal":
                case "leftright":
                case "lefttoright":
                    p0 = new SKPoint(0, size / 2f);
                    p1 = new SKPoint(size, size / 2f);
                    break;
                case "diagonalup":
                case "bottomlefttotopright":
                    p0 = new SKPoint(0, size);
                    p1 = new SKPoint(size, 0);
                    break;
                case "diagonal":
                default:
                    p0 = new SKPoint(0, 0);
                    p1 = new SKPoint(size, size);
                    break;
            }

            using var shader = SKShader.CreateLinearGradient(
                p0,
                p1,
                new[] { startColor, endColor },
                new[] { 0.0f, 1.0f },
                SKShaderTileMode.Clamp);

            bgPaint.Shader = shader;
            canvas.DrawRoundRect(roundRect, bgPaint);
        }
        else
        {
            bgPaint.Color = primaryColor;
            canvas.DrawRoundRect(roundRect, bgPaint);
        }
    }

    private static SKColor Lerp(SKColor a, SKColor b, float t)
    {
        byte r = (byte)(a.Red + (b.Red - a.Red) * t);
        byte g = (byte)(a.Green + (b.Green - a.Green) * t);
        byte bl = (byte)(a.Blue + (b.Blue - a.Blue) * t);
        byte alpha = (byte)(a.Alpha + (b.Alpha - a.Alpha) * t);
        return new SKColor(r, g, bl, alpha);
    }

    private void DrawGradientBackground(SKCanvas canvas, string colorHex, int size)
    {
        DrawBackground(canvas, colorHex, null, "Gradient", "Diagonal", size);
    }

    private void DrawScaledPicture(SKCanvas canvas, SKPicture picture, float x, float y, float targetWidth, float targetHeight, SKColor? tintColor)
    {
        var bounds = picture.CullRect;
        if (bounds.Width <= 0 || bounds.Height <= 0) return;

        float scale = Math.Min(targetWidth / bounds.Width, targetHeight / bounds.Height);
        float tx = x + (targetWidth - (bounds.Width * scale)) / 2f - (bounds.Left * scale);
        float ty = y + (targetHeight - (bounds.Height * scale)) / 2f - (bounds.Top * scale);

        var matrix = SKMatrix.CreateTranslation(tx, ty);
        matrix = SKMatrix.Concat(matrix, SKMatrix.CreateScale(scale, scale));

        using var paint = new SKPaint
        {
            IsAntialias = true,
            FilterQuality = SKFilterQuality.High,
            IsDither = true
        };

        if (tintColor.HasValue)
        {
            paint.ColorFilter = SKColorFilter.CreateBlendMode(tintColor.Value, SKBlendMode.SrcIn);
        }

        canvas.DrawPicture(picture, ref matrix, paint);
    }

    private void DrawLabelText(SKCanvas canvas, string text, int size)
    {
        using var textPaint = new SKPaint
        {
            Color = SKColors.White,
            IsAntialias = true,
            SubpixelText = true,
            LcdRenderText = true,
            FilterQuality = SKFilterQuality.High,
            FakeBoldText = true,
            TextAlign = SKTextAlign.Center,
            Typeface = SKTypeface.FromFamilyName("Segoe UI", SKFontStyle.Bold)
        };

        float fontSize = size * 0.45f;
        textPaint.TextSize = fontSize;
        float textWidth = textPaint.MeasureText(text);
        while (textWidth > size * 0.85f && fontSize > 12)
        {
            fontSize -= 4;
            textPaint.TextSize = fontSize;
            textWidth = textPaint.MeasureText(text);
        }

        var textBounds = new SKRect();
        textPaint.MeasureText(text, ref textBounds);
        float textX = size / 2f;
        float textY = (size / 2f) - textBounds.MidY;
        canvas.DrawText(text, textX, textY, textPaint);
    }

    private SKSvg? LoadSvgContent(string input, string? rootDir)
    {
        if (string.IsNullOrWhiteSpace(input)) return null;

        string trimmed = input.Trim();

        // Check if it's a file path
        string? resolvedFilePath = null;
        if (File.Exists(trimmed))
        {
            resolvedFilePath = trimmed;
        }
        else if (!string.IsNullOrEmpty(rootDir))
        {
            string combined = Path.IsPathRooted(trimmed) ? trimmed : Path.GetFullPath(Path.Combine(rootDir, trimmed));
            if (File.Exists(combined))
            {
                resolvedFilePath = combined;
            }
        }

        var svg = new SKSvg();
        if (resolvedFilePath != null)
        {
            svg.Load(resolvedFilePath);
            return svg;
        }

        // If it's raw XML
        if (trimmed.StartsWith("<", StringComparison.OrdinalIgnoreCase))
        {
            svg.FromSvg(trimmed);
            return svg;
        }

        // If it's SVG path data (e.g. M10 10 H 90 ...)
        string wrappedSvg = $@"<svg xmlns=""http://www.w3.org/2000/svg"" viewBox=""0 0 100 100""><path d=""{trimmed}"" fill=""white"" /></svg>";
        svg.FromSvg(wrappedSvg);
        return svg;
    }

    public static BitmapSource ToBitmapSource(SKBitmap bitmap)
    {
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = new MemoryStream(data.ToArray());

        var bi = new BitmapImage();
        bi.BeginInit();
        bi.CacheOption = BitmapCacheOption.OnLoad;
        bi.StreamSource = stream;
        bi.EndInit();
        bi.Freeze();
        return bi;
    }

    private string FetchBootstrapIconSvg(string name, string iconsBaseDir)
    {
        string cacheDir = Path.Combine(iconsBaseDir, "_bootstrap_cache");
        if (!Directory.Exists(cacheDir))
        {
            Directory.CreateDirectory(cacheDir);
        }
        string cacheFile = Path.Combine(cacheDir, $"{name}.svg");

        if (File.Exists(cacheFile))
        {
            return File.ReadAllText(cacheFile);
        }

        try
        {
            using var client = new HttpClient();
            client.DefaultRequestHeaders.Add("User-Agent", "CatalystIconGenerator");
            string url = $"https://cdn.jsdelivr.net/npm/bootstrap-icons@1.11.3/icons/{name}.svg";
            var response = client.GetAsync(url).GetAwaiter().GetResult();
            if (response.IsSuccessStatusCode)
            {
                string svg = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                File.WriteAllText(cacheFile, svg);
                return svg;
            }
        }
        catch { }

        return string.Empty;
    }

    private void SaveAsIco(List<byte[]> images, List<(int Width, int Height)> dimensions, string outputPath)
    {
        using var stream = File.Create(outputPath);
        using var writer = new BinaryWriter(stream);

        // ICO Header
        writer.Write((short)0);    // Reserved
        writer.Write((short)1);    // Type (1 for Icon)
        writer.Write((short)images.Count); // Number of images

        int offset = 6 + (images.Count * 16);

        for (int i = 0; i < images.Count; i++)
        {
            var (width, height) = dimensions[i];
            var data = images[i];

            writer.Write((byte)(width >= 256 ? 0 : width));
            writer.Write((byte)(height >= 256 ? 0 : height));
            writer.Write((byte)0); // Color palette
            writer.Write((byte)0); // Reserved
            writer.Write((short)1); // Color planes
            writer.Write((short)32); // Bits per pixel
            writer.Write(data.Length); // Size of image data
            writer.Write(offset); // Offset to image data

            offset += data.Length;
        }

        foreach (var data in images)
        {
            writer.Write(data);
        }
    }
}
