using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace Catalyst.Services;

public class HotkeyManager : IDisposable
{
    private const int WM_HOTKEY = 0x0312;
    private const uint MOD_ALT = 0x0001;
    private const uint MOD_CONTROL = 0x0002;
    private const uint MOD_SHIFT = 0x0004;
    private const uint MOD_WIN = 0x0008;
    private const uint MOD_NOREPEAT = 0x4000;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    private IntPtr _hwnd;
    private HwndSource? _source;
    private int _currentId = 9000;
    private readonly Dictionary<int, Action> _callbacks = new();

    public string CurrentHotkey { get; private set; } = string.Empty;

    public bool Register(Window window, string hotkeyString, Action callback, out string registeredHotkey)
    {
        registeredHotkey = hotkeyString;
        if (string.IsNullOrWhiteSpace(hotkeyString))
        {
            hotkeyString = "Alt+Space";
        }

        if (_hwnd == IntPtr.Zero)
        {
            var helper = new WindowInteropHelper(window);
            _hwnd = helper.EnsureHandle();
            _source = HwndSource.FromHwnd(_hwnd);
            _source?.AddHook(HwndHook);
        }

        UnregisterAll();

        // 1. Try requested hotkey
        if (TryParseHotkey(hotkeyString, out uint modifiers, out uint vk))
        {
            int id = ++_currentId;
            if (RegisterHotKey(_hwnd, id, modifiers | MOD_NOREPEAT, vk))
            {
                _callbacks[id] = callback;
                CurrentHotkey = hotkeyString;
                registeredHotkey = hotkeyString;
                return true;
            }
        }

        // 2. Fallbacks if requested hotkey is already occupied
        string[] fallbacks = { "Ctrl+Shift+Space", "Ctrl+Alt+Space", "Alt+OemTilde", "Ctrl+Shift+C" };
        foreach (var fb in fallbacks)
        {
            if (fb.Equals(hotkeyString, StringComparison.OrdinalIgnoreCase)) continue;

            if (TryParseHotkey(fb, out uint fbMod, out uint fbVk))
            {
                int id = ++_currentId;
                if (RegisterHotKey(_hwnd, id, fbMod | MOD_NOREPEAT, fbVk))
                {
                    _callbacks[id] = callback;
                    CurrentHotkey = fb;
                    registeredHotkey = fb;
                    Debug.WriteLine($"Primary hotkey failed; registered fallback hotkey: {fb}");
                    return true;
                }
            }
        }

        return false;
    }

    public static bool TryParseHotkey(string hotkeyStr, out uint modifiers, out uint vk)
    {
        modifiers = 0;
        vk = 0;
        if (string.IsNullOrWhiteSpace(hotkeyStr)) return false;

        var parts = hotkeyStr.Split(new[] { '+', ' ' }, StringSplitOptions.RemoveEmptyEntries);
        Key key = Key.None;

        foreach (var part in parts)
        {
            var p = part.Trim().ToLowerInvariant();
            if (p is "alt" or "menu") modifiers |= MOD_ALT;
            else if (p is "ctrl" or "control") modifiers |= MOD_CONTROL;
            else if (p is "shift") modifiers |= MOD_SHIFT;
            else if (p is "win" or "windows" or "super") modifiers |= MOD_WIN;
            else
            {
                if (Enum.TryParse<Key>(part, true, out var parsedKey))
                {
                    key = parsedKey;
                }
                else if (part.Length == 1 && char.IsLetterOrDigit(part[0]))
                {
                    if (Enum.TryParse<Key>(part.ToUpperInvariant(), true, out var letterKey))
                    {
                        key = letterKey;
                    }
                }
                else if (p is "space" or "spacebar")
                {
                    key = Key.Space;
                }
                else if (p is "`" or "~" or "backquote" or "tilde" or "oemtilde")
                {
                    key = Key.OemTilde;
                }
            }
        }

        if (key == Key.None) return false;
        vk = (uint)KeyInterop.VirtualKeyFromKey(key);
        return vk != 0;
    }

    public void UnregisterAll()
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
        UnregisterAll();
        if (_source != null)
        {
            _source.RemoveHook(HwndHook);
            _source = null;
        }
        _hwnd = IntPtr.Zero;
    }
}
