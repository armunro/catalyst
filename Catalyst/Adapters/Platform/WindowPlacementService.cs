using System.Windows;
using Catalyst.Core.Ports.Outbound;

namespace Catalyst.Adapters.Platform;

public class WindowPlacementService : IWindowPlacementService
{
    public void PositionBottomRight(Window window)
    {
        var workingArea = SystemParameters.WorkArea;
        window.Left = workingArea.Right - window.Width - 10;
        window.Top = workingArea.Bottom - window.Height - 10;
    }
}
