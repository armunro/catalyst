using Catalyst.Core.Domain.Models;

namespace Catalyst.Core.Ports.Outbound;

public interface ISettingsStorage
{
    string SettingsFilePath { get; set; }
    UserSettings? LoadSettings();
    void SaveSettings(UserSettings settings);
}
