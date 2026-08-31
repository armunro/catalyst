using System;
using System.Windows;

namespace Catalyst.Core.Ports.Outbound;

public interface IGlobalHotkeyHook
{
    bool Register(Window window, uint modifiers, uint virtualKey, Action callback);
    void Unregister();
}
