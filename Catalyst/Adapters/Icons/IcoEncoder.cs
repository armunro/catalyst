using System.Collections.Generic;
using System.IO;

namespace Catalyst.Adapters.Icons;

public static class IcoEncoder
{
    public static void SaveAsIco(List<byte[]> images, List<(int Width, int Height)> dimensions, string outputPath)
    {
        var dir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        using var stream = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite);
        using var writer = new BinaryWriter(stream);

        // ICO Header
        writer.Write((short)0);            // Reserved
        writer.Write((short)1);            // Type (1 for Icon)
        writer.Write((short)images.Count); // Number of images

        int offset = 6 + (images.Count * 16);

        for (int i = 0; i < images.Count; i++)
        {
            var (width, height) = dimensions[i];
            var data = images[i];

            writer.Write((byte)(width >= 256 ? 0 : width));
            writer.Write((byte)(height >= 256 ? 0 : height));
            writer.Write((byte)0);      // Color palette
            writer.Write((byte)0);      // Reserved
            writer.Write((short)1);     // Color planes
            writer.Write((short)32);    // Bits per pixel
            writer.Write(data.Length);  // Size of image data
            writer.Write(offset);       // Offset to image data

            offset += data.Length;
        }

        foreach (var data in images)
        {
            writer.Write(data);
        }
    }
}
