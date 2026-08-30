using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace Catalyst.WPF;

public static class ThemeHelper
{
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    private const int DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1 = 19;
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

    public static bool ApplyDarkTheme(Window window)
    {
        var helper = new WindowInteropHelper(window);
        IntPtr hwnd = helper.Handle;

        if (hwnd == IntPtr.Zero)
        {
            // If the handle isn't available yet, wait for the SourceInitialized event
            window.SourceInitialized += (s, e) => ApplyDarkTheme(window);
            return false;
        }

        int attribute = DWMWA_USE_IMMERSIVE_DARK_MODE;
        if (Environment.OSVersion.Version.Build < 18985)
        {
            attribute = DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1;
        }

        int useImmersiveDarkMode = 1;
        int result = DwmSetWindowAttribute(hwnd, attribute, ref useImmersiveDarkMode, sizeof(int));
        return result == 0;
    }
}
