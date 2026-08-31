using System;
using System.Collections.Generic;
using System.Windows.Media.Imaging;
using SkiaSharp;

namespace Catalyst.Core.Ports.Outbound;

public interface IIconRenderer
{
    SKBitmap RenderIconBitmap(AppInfo app, int size, string iconsBaseDir, string? rootDir = null, Action<string>? logger = null);
    BitmapSource? RenderPreview(AppInfo app, string iconsBaseDir, string? rootDir = null, int size = 512);
    void SavePng(SKBitmap bitmap, string outputPath);
    void SaveAsIco(List<byte[]> pngByteArrays, List<(int Width, int Height)> dimensions, string outputPath);
}
