using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Catalyst.Core.Ports.Outbound;

namespace Catalyst.Adapters.Platform;

public class WindowsHotkeyHook : IGlobalHotkeyHook, IDisposable
{
    private const int WM_HOTKEY = 0x0312;
    private const uint MOD_NOREPEAT = 0x4000;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    private IntPtr _hwnd;
    private HwndSource? _source;
    private int _currentId = 9000;
    private readonly Dictionary<int, Action> _callbacks = new();

    public bool Register(Window window, uint modifiers, uint virtualKey, Action callback)
    {
        if (_hwnd == IntPtr.Zero)
        {
            var helper = new WindowInteropHelper(window);
            _hwnd = helper.EnsureHandle();
            _source = HwndSource.FromHwnd(_hwnd);
            _source?.AddHook(HwndHook);
        }

        Unregister();

        int id = ++_currentId;
        if (RegisterHotKey(_hwnd, id, modifiers | MOD_NOREPEAT, virtualKey))
        {
            _callbacks[id] = callback;
            return true;
        }

        return false;
    }

    public void Unregister()
    {
        if (_hwnd != IntPtr.Zero)
        {
            foreach (var id in _callbacks.Keys)
            {
                UnregisterHotKey(_hwnd, id);
            }
            _callbacks.Clear();
        }
    }

    private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY)
        {
            int id = wParam.ToInt32();
            if (_callbacks.TryGetValue(id, out var action))
            {
                action?.Invoke();
                handled = true;
            }
        }
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        Unregister();
        if (_source != null)
        {
            _source.RemoveHook(HwndHook);
            _source = null;
        }
        _hwnd = IntPtr.Zero;
    }
}
