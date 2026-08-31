using System;
using System.Collections.Generic;
using System.Windows.Media.Imaging;

namespace Catalyst.Core.Ports.Inbound;

public interface IIconManagementService
{
    void GenerateIcon(AppInfo app, Action<string>? logger = null);
    void GenerateAllIcons(IEnumerable<AppInfo> apps, Action<string>? logger = null);
    BitmapSource? RenderPreview(AppInfo app, int size = 512);
}
