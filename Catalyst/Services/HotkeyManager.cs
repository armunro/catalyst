using System;
using System.Windows;
using Catalyst.Adapters.Platform;
using Catalyst.Core.Ports.Inbound;
using Catalyst.Core.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Catalyst.Services;

/// <summary>
/// Backward-compatible facade delegating to hexagonal hotkey service.
/// </summary>
public class HotkeyManager : IDisposable
{
    private readonly IHotkeyService _hotkeyService;

    public HotkeyManager()
    {
        _hotkeyService = App.Services?.GetService<IHotkeyService>() ?? new HotkeyService(new WindowsHotkeyHook());
    }

    public string CurrentHotkey { get; private set; } = string.Empty;

    public bool Register(Window window, string hotkeyString, Action callback, out string registeredHotkey)
    {
        registeredHotkey = hotkeyString;
        bool success = _hotkeyService.RegisterHotkey(window, hotkeyString, callback);
        if (success)
        {
            CurrentHotkey = hotkeyString;
        }
        return success;
    }

    public static bool TryParseHotkey(string hotkeyStr, out uint modifiers, out uint vk)
    {
        var service = App.Services?.GetService<IHotkeyService>() ?? new HotkeyService(new WindowsHotkeyHook());
        return service.TryParseHotkey(hotkeyStr, out modifiers, out vk);
    }

    public void UnregisterAll()
    {
        _hotkeyService.UnregisterHotkey();
    }

    public void Dispose()
    {
        UnregisterAll();
    }
}
