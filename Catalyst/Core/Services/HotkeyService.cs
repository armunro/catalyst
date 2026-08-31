using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using Catalyst.Core.Ports.Inbound;
using Catalyst.Core.Ports.Outbound;

namespace Catalyst.Core.Services;

public class HotkeyService : IHotkeyService
{
    private const uint MOD_ALT = 0x0001;
    private const uint MOD_CONTROL = 0x0002;
    private const uint MOD_SHIFT = 0x0004;
    private const uint MOD_WIN = 0x0008;

    private readonly IGlobalHotkeyHook _hotkeyHook;

    public HotkeyService(IGlobalHotkeyHook hotkeyHook)
    {
        _hotkeyHook = hotkeyHook;
    }

    public bool RegisterHotkey(Window window, string hotkeyString, Action callback)
    {
        if (string.IsNullOrWhiteSpace(hotkeyString))
        {
            hotkeyString = "Alt+Space";
        }

        // 1. Try requested hotkey
        if (TryParseHotkey(hotkeyString, out uint modifiers, out uint vk))
        {
            if (_hotkeyHook.Register(window, modifiers, vk, callback))
            {
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
                if (_hotkeyHook.Register(window, fbMod, fbVk, callback))
                {
                    Debug.WriteLine($"Primary hotkey failed; registered fallback hotkey: {fb}");
                    return true;
                }
            }
        }

        return false;
    }

    public void UnregisterHotkey()
    {
        _hotkeyHook.Unregister();
    }

    public bool TryParseHotkey(string hotkey, out uint modifiers, out uint virtualKey)
    {
        modifiers = 0;
        virtualKey = 0;
        if (string.IsNullOrWhiteSpace(hotkey)) return false;

        var parts = hotkey.Split(new[] { '+', ' ' }, StringSplitOptions.RemoveEmptyEntries);
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
        virtualKey = (uint)KeyInterop.VirtualKeyFromKey(key);
        return virtualKey != 0;
    }
}
