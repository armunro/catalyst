using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media.Imaging;
using Wpf.Ui.Controls;

namespace Catalyst.Windows;

public partial class IconsResultWindow : FluentWindow
{
    private string _iconsPath;

    public IconsResultWindow(string iconsPath, List<(string AppName, string IconPath)> generatedIcons)
    {
        _iconsPath = iconsPath;
        InitializeComponent();
        
        // Convert to a format suitable for binding
        var items = generatedIcons
            .Where(x => File.Exists(x.IconPath))
            .Select(x => {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.UriSource = new Uri(Path.GetFullPath(x.IconPath));
                bitmap.EndInit();
                bitmap.Freeze();
                return new { x.AppName, IconSource = bitmap };
            })
            .ToList();
            
        IconsItemsControl.ItemsSource = items;
    }

    private void BtnOpenFolder_Click(object sender, RoutedEventArgs e)
    {
        if (Directory.Exists(_iconsPath))
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = Path.GetFullPath(_iconsPath),
                UseShellExecute = true
            });
        }
    }
}