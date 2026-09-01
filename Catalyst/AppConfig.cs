using System.Collections.Generic;

namespace Catalyst;

public class AppConfigFile
{
    public string Hotkey { get; set; } = "Alt+Space";
    public List<AppConfigEntry> Apps { get; set; } = new();
}

public class AppConfigEntry
{
    public string Name { get; set; } = string.Empty;
    public bool Hidden { get; set; } = false;
    public string CatalystDirectory { get; set; } = string.Empty;
    public LaunchConfig Launch { get; set; } = new();
    public IconConfig Icon { get; set; } = new();
}

public class LaunchConfig
{
    public string ProjectPath { get; set; } = string.Empty;
    public string ExecutablePath { get; set; } = string.Empty;
    public string Arguments { get; set; } = string.Empty;
    public string WorkingDirectory { get; set; } = string.Empty;
    public bool RunAsAdmin { get; set; } = false;
}

public class IconConfig
{
    public string Color { get; set; } = "#1E88E4";
    public string SecondaryColor { get; set; } = string.Empty;
    public string BackgroundType { get; set; } = "Solid";
    public string GradientDirection { get; set; } = "Diagonal";
    public string Label { get; set; } = string.Empty;
    public string IconPath { get; set; } = string.Empty;
    public string FaviconPath { get; set; } = string.Empty;
    public string BootstrapIcon { get; set; } = string.Empty;
    public string CustomGlyphSvg { get; set; } = string.Empty;
    public string CustomGlyphColor { get; set; } = string.Empty;
    public string SvgOverride { get; set; } = string.Empty;
}
