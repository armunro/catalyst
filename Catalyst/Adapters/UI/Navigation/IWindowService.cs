using System.Collections.Generic;
using System.Windows;

namespace Catalyst.Adapters.UI.Navigation;

public interface IWindowService
{
    void ShowMainWindow();
    void ShowAppManagement(Window? owner = null);
    void ShowLogViewer(AppInfo app, Window? owner = null);
    void ShowIconsResult(string iconsPath, List<(string AppName, string IconPath)> generatedIcons, Window? owner = null);
    void PositionBottomRight(Window window);
}
