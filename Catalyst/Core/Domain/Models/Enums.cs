namespace Catalyst.Core.Domain.Models;

public enum IconMode
{
    Auto,
    Bootstrap,
    CustomGlyph,
    SvgOverride,
    Label
}

public enum BackgroundType
{
    Solid,
    Gradient
}

public enum GradientDirection
{
    Diagonal,
    Vertical,
    Horizontal,
    DiagonalUp
}

public enum LaunchTargetType
{
    None,
    DotnetProject,
    Executable,
    BatchScript,
    PowerShellScript,
    WebUrl,
    GeneralApplication
}
