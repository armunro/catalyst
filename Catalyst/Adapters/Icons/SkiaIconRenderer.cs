using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Windows.Media.Imaging;
using System.Xml.Linq;
using Catalyst.Core.Ports.Outbound;
using SkiaSharp;
using Svg.Skia;

namespace Catalyst.Adapters.Icons;

public class SkiaIconRenderer : IIconRenderer
{
    private static readonly HttpClient HttpClient = new()
    {
        DefaultRequestHeaders = { { "User-Agent", "CatalystIconGenerator" } },
        Timeout = TimeSpan.FromSeconds(5)
    };

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

        var scaled = new SKBitmap(size, size, SKColorType.Rgba8888, SKAlphaType.Premul);
        if (highResBitmap.ScalePixels(scaled.PeekPixels(), SKFilterQuality.High))
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

    public void SavePng(SKBitmap bitmap, string outputPath)
    {
        var dir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite);
        data.SaveTo(stream);
    }

    public void SaveAsIco(List<byte[]> pngByteArrays, List<(int Width, int Height)> dimensions, string outputPath)
    {
        IcoEncoder.SaveAsIco(pngByteArrays, dimensions, outputPath);
    }

    public string RenderIconSvg(AppInfo app, int size, string iconsBaseDir, string? rootDir = null, Action<string>? logger = null)
    {
        using var ms = new MemoryStream();
        using (var canvas = SKSvgCanvas.Create(new SKRect(0, 0, size, size), ms))
        {
            RenderToCanvas(canvas, app, size, iconsBaseDir, rootDir, logger);
        }
        string svg = System.Text.Encoding.UTF8.GetString(ms.ToArray());
        if (!svg.Contains("viewBox=", StringComparison.OrdinalIgnoreCase))
        {
            int index = svg.IndexOf("<svg", StringComparison.OrdinalIgnoreCase);
            if (index >= 0)
            {
                int insertPos = index + 4;
                svg = svg.Insert(insertPos, $" viewBox=\"0 0 {size} {size}\"");
            }
        }
        return svg;
    }

    public void SaveSvg(AppInfo app, string outputPath, int size, string iconsBaseDir, string? rootDir = null, Action<string>? logger = null)
    {
        var dir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        string svg = RenderIconSvg(app, size, iconsBaseDir, rootDir, logger);
        File.WriteAllText(outputPath, svg, System.Text.Encoding.UTF8);
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

    private void RenderToCanvas(SKCanvas canvas, AppInfo app, int size, string iconsBaseDir, string? rootDir = null, Action<string>? logger = null)
    {
        string? projectDir = Catalyst.Core.Services.AppConfigurationService.GetProjectDirectory(app.ProjectPath, app.WorkingDirectory, rootDir, app.ExecutablePath);

        // CASE 1: Complete SVG Override
        if (!string.IsNullOrWhiteSpace(app.SvgOverride))
        {
            try
            {
                var svg = LoadSvgContent(app.SvgOverride, projectDir, rootDir);
                if (svg?.Picture != null)
                {
                    DrawScaledPicture(canvas, svg.Picture, 0, 0, size, size, null);
                    return;
                }
            }
            catch (Exception ex)
            {
                logger?.Invoke($"Error rendering SVG override for {app.Name}: {ex.Message}");
            }
        }

        // CASE 2: Background + (Custom Glyph OR Bootstrap Icon OR Text Label)
        DrawBackground(canvas, app.Color, app.SecondaryColor, app.BackgroundType, app.GradientDirection, size);

        // 2a. Custom Glyph SVG
        if (!string.IsNullOrWhiteSpace(app.CustomGlyphSvg))
        {
            try
            {
                SKColor? glyphColor = null;
                if (!string.IsNullOrWhiteSpace(app.CustomGlyphColor) && SKColor.TryParse(app.CustomGlyphColor, out var parsedGlyphColor))
                {
                    glyphColor = parsedGlyphColor;
                }

                var svg = LoadSvgContent(app.CustomGlyphSvg, projectDir, rootDir, glyphColor);
                if (svg?.Picture != null)
                {
                    float paddingFraction = GetPaddingFraction(app.Padding);
                    float padding = size * paddingFraction;
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
                    iconName = iconName[..^4];
                }

                string svgContent = FetchBootstrapIconSvg(iconName, iconsBaseDir);
                if (!string.IsNullOrEmpty(svgContent))
                {
                    SKColor bootstrapColor = SKColors.White;
                    if (!string.IsNullOrWhiteSpace(app.CustomGlyphColor) && SKColor.TryParse(app.CustomGlyphColor, out var parsedBootstrapColor))
                    {
                        bootstrapColor = parsedBootstrapColor;
                    }

                    string tintedSvg = ApplyTintToSvg(svgContent, bootstrapColor);
                    var svg = new SKSvg();
                    svg.FromSvg(tintedSvg);
                    if (svg.Picture != null)
                    {
                        float paddingFraction = GetPaddingFraction(app.Padding);
                        float padding = size * paddingFraction;
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
                SKColor labelColor = SKColors.White;
                if (!string.IsNullOrWhiteSpace(app.CustomGlyphColor) && SKColor.TryParse(app.CustomGlyphColor, out var parsedLabelColor))
                {
                    labelColor = parsedLabelColor;
                }
                DrawLabelText(canvas, app.Label, size, labelColor, GetPaddingFraction(app.Padding));
            }
            catch (Exception ex)
            {
                logger?.Invoke($"Error rendering label text for {app.Name}: {ex.Message}");
            }
        }
    }

    private void DrawBackground(SKCanvas canvas, string primaryColorHex, string? secondaryColorHex, string? backgroundType, string? gradientDirection, int size)
    {
        if (!SKColor.TryParse(primaryColorHex, out var primaryColor))
        {
            primaryColor = SKColor.Parse("#1E88E4");
        }

        using var bgPaint = new SKPaint
        {
            IsAntialias = true,
            FilterQuality = SKFilterQuality.High,
            IsDither = true,
            Style = SKPaintStyle.Fill
        };

        float cornerRadius = size * 0.2f;
        var roundRect = new SKRoundRect(new SKRect(0, 0, size, size), cornerRadius, cornerRadius);

        bool isGradient = string.Equals(backgroundType, "Gradient", StringComparison.OrdinalIgnoreCase) ||
                          string.Equals(backgroundType, "2-Part Gradient", StringComparison.OrdinalIgnoreCase) ||
                          (!string.IsNullOrWhiteSpace(secondaryColorHex) && !string.Equals(backgroundType, "Solid", StringComparison.OrdinalIgnoreCase));

        if (isGradient)
        {
            SKColor startColor = primaryColor;
            SKColor endColor;

            if (!string.IsNullOrWhiteSpace(secondaryColorHex) && SKColor.TryParse(secondaryColorHex, out var parsedSecondaryColor))
            {
                endColor = parsedSecondaryColor;
            }
            else
            {
                startColor = Lerp(primaryColor, SKColors.White, 0.35f);
                endColor = Lerp(primaryColor, SKColors.Black, 0.25f);
            }

            SKPoint p0;
            SKPoint p1;

            string dir = (gradientDirection ?? "Diagonal").ToLowerInvariant();
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

    private static float GetPaddingFraction(int? padding)
    {
        if (!padding.HasValue)
        {
            return 0.16f;
        }

        float pad = Math.Clamp(padding.Value, 0, 49);
        return pad / 100f;
    }

    private void DrawLabelText(SKCanvas canvas, string text, int size, SKColor? textColor = null, float? paddingFraction = null)
    {
        using var textPaint = new SKPaint
        {
            Color = textColor ?? SKColors.White,
            IsAntialias = true,
            SubpixelText = true,
            LcdRenderText = true,
            FilterQuality = SKFilterQuality.High,
            FakeBoldText = true,
            TextAlign = SKTextAlign.Center,
            Typeface = SKTypeface.FromFamilyName("Segoe UI", SKFontStyle.Bold)
        };

        float padFrac = paddingFraction ?? 0.16f;
        float maxAllowedWidth = size * (1.0f - (padFrac * 2));
        if (maxAllowedWidth < 10) maxAllowedWidth = size * 0.85f;

        float fontSize = size * 0.45f;
        textPaint.TextSize = fontSize;
        float textWidth = textPaint.MeasureText(text);
        while (textWidth > maxAllowedWidth && fontSize > 12)
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

    private SKSvg? LoadSvgContent(string input, string? projectDir, string? rootDir, SKColor? tintColor = null)
    {
        if (string.IsNullOrWhiteSpace(input)) return null;

        string trimmed = input.Trim();

        string? resolvedFilePath = null;
        if (Path.IsPathRooted(trimmed))
        {
            if (File.Exists(trimmed))
            {
                resolvedFilePath = trimmed;
            }
        }
        else
        {
            if (!string.IsNullOrEmpty(projectDir))
            {
                string combined = Path.GetFullPath(Path.Combine(projectDir, trimmed));
                if (File.Exists(combined))
                {
                    resolvedFilePath = combined;
                }
            }

            if (resolvedFilePath == null && !string.IsNullOrEmpty(rootDir))
            {
                string combined = Path.GetFullPath(Path.Combine(rootDir, trimmed));
                if (File.Exists(combined))
                {
                    resolvedFilePath = combined;
                }
            }

            if (resolvedFilePath == null && File.Exists(trimmed))
            {
                resolvedFilePath = Path.GetFullPath(trimmed);
            }
        }

        var svg = new SKSvg();
        if (resolvedFilePath != null)
        {
            string fileContent = File.ReadAllText(resolvedFilePath);
            if (tintColor.HasValue)
            {
                fileContent = ApplyTintToSvg(fileContent, tintColor.Value);
            }
            svg.FromSvg(fileContent);
            return svg;
        }

        if (trimmed.StartsWith("<", StringComparison.OrdinalIgnoreCase))
        {
            string content = trimmed;
            if (tintColor.HasValue)
            {
                content = ApplyTintToSvg(content, tintColor.Value);
            }
            svg.FromSvg(content);
            return svg;
        }

        string hexColor = tintColor.HasValue ? $"#{tintColor.Value.Red:X2}{tintColor.Value.Green:X2}{tintColor.Value.Blue:X2}" : "white";
        string wrappedSvg = $@"<svg xmlns=""http://www.w3.org/2000/svg"" viewBox=""0 0 100 100""><path d=""{trimmed}"" fill=""{hexColor}"" /></svg>";
        svg.FromSvg(wrappedSvg);
        return svg;
    }

    private static string ApplyTintToSvg(string svgContent, SKColor tintColor)
    {
        if (string.IsNullOrWhiteSpace(svgContent)) return svgContent;

        string hexColor = $"#{tintColor.Red:X2}{tintColor.Green:X2}{tintColor.Blue:X2}";

        try
        {
            var doc = XDocument.Parse(svgContent);
            var root = doc.Root;
            if (root != null)
            {
                foreach (var elem in doc.Descendants())
                {
                    var fillAttr = elem.Attribute("fill");
                    if (fillAttr != null)
                    {
                        string val = fillAttr.Value.Trim();
                        if (string.Equals(val, "currentColor", StringComparison.OrdinalIgnoreCase) ||
                            (!string.Equals(val, "none", StringComparison.OrdinalIgnoreCase) &&
                             !string.Equals(val, "transparent", StringComparison.OrdinalIgnoreCase) &&
                             !val.StartsWith("url(", StringComparison.OrdinalIgnoreCase)))
                        {
                            fillAttr.Value = hexColor;
                        }
                    }

                    var strokeAttr = elem.Attribute("stroke");
                    if (strokeAttr != null)
                    {
                        string val = strokeAttr.Value.Trim();
                        if (string.Equals(val, "currentColor", StringComparison.OrdinalIgnoreCase))
                        {
                            strokeAttr.Value = hexColor;
                        }
                    }

                    var styleAttr = elem.Attribute("style");
                    if (styleAttr != null)
                    {
                        string styleVal = styleAttr.Value;
                        styleVal = Regex.Replace(
                            styleVal,
                            @"(fill\s*:\s*)(?:currentColor|#[0-9a-fA-F]{3,8}|rgba?\([^)]+\)|[a-zA-Z]+)",
                            m => m.Value.Contains("none", StringComparison.OrdinalIgnoreCase) || m.Value.Contains("transparent", StringComparison.OrdinalIgnoreCase) || m.Value.Contains("url", StringComparison.OrdinalIgnoreCase) ? m.Value : $"{m.Groups[1].Value}{hexColor}",
                            RegexOptions.IgnoreCase);

                        styleVal = Regex.Replace(
                            styleVal,
                            @"(stroke\s*:\s*)(?:currentColor)",
                            $"$1{hexColor}",
                            RegexOptions.IgnoreCase);

                        styleAttr.Value = styleVal;
                    }
                }

                var rootFill = root.Attribute("fill");
                if (rootFill == null)
                {
                    root.SetAttributeValue("fill", hexColor);
                }

                return doc.ToString();
            }
        }
        catch
        {
            // Fallback for non-standard XML or fragments
        }

        string modified = Regex.Replace(
            svgContent,
            @"fill\s*=\s*""(?:currentColor|#[0-9a-fA-F]{3,8}|[a-zA-Z]+)""",
            m => m.Value.Contains("none", StringComparison.OrdinalIgnoreCase) || m.Value.Contains("transparent", StringComparison.OrdinalIgnoreCase) ? m.Value : $"fill=\"{hexColor}\"",
            RegexOptions.IgnoreCase);

        modified = Regex.Replace(
            modified,
            @"stroke\s*=\s*""currentColor""",
            $"stroke=\"{hexColor}\"",
            RegexOptions.IgnoreCase);

        if (!modified.Contains("fill=", StringComparison.OrdinalIgnoreCase))
        {
            int svgTag = modified.IndexOf("<svg", StringComparison.OrdinalIgnoreCase);
            if (svgTag >= 0)
            {
                modified = modified.Insert(svgTag + 4, $" fill=\"{hexColor}\"");
            }
        }

        return modified;
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
            string url = $"https://cdn.jsdelivr.net/npm/bootstrap-icons@1.11.3/icons/{name}.svg";
            var response = HttpClient.GetAsync(url).GetAwaiter().GetResult();
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
}
