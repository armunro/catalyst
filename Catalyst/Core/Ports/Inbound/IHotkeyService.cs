using System;
using System.Windows;

namespace Catalyst.Core.Ports.Inbound;

public interface IHotkeyService
{
    bool RegisterHotkey(Window window, string hotkeyString, Action callback);
    void UnregisterHotkey();
    bool TryParseHotkey(string hotkey, out uint modifiers, out uint virtualKey);
}
