using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Catalyst;

public enum IconMode
{
    Auto,
    Bootstrap,
    CustomGlyph,
    SvgOverride,
    Label
}

public class AppInfo : INotifyPropertyChanged
{
    private string _name = string.Empty;
    private string _projectPath = string.Empty;
    private string _executablePath = string.Empty;
    private string _arguments = string.Empty;
    private string _workingDirectory = string.Empty;
    private bool _runAsAdmin = false;
    private string _color = "#1E88E4";
    private string _secondaryColor = string.Empty;
    private string _backgroundType = "Solid";
    private string _gradientDirection = "Diagonal";
    private string _label = string.Empty;
    private string _faviconPath = string.Empty;
    private string _bootstrapIcon = string.Empty;
    private string _customGlyphSvg = string.Empty;
    private string _customGlyphColor = string.Empty;
    private string _svgOverride = string.Empty;
    private string _iconPath = string.Empty;
    private string _shortcutIndex = string.Empty;
    private bool _isHidden = false;
    private Process? _process;
    private BitmapSource? _previewSource;

    public string Name 
    { 
        get => _name; 
        set { _name = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsRunningString)); } 
    }

    public string ProjectPath 
    { 
        get => _projectPath; 
        set 
        { 
            _projectPath = value; 
            OnPropertyChanged(); 
            OnPropertyChanged(nameof(LaunchTarget));
            OnPropertyChanged(nameof(HasProjectPath)); 
            OnPropertyChanged(nameof(IsLaunchable));
            OnPropertyChanged(nameof(ShortProjectPath)); 
            OnPropertyChanged(nameof(ShortLaunchTarget));
            OnPropertyChanged(nameof(LaunchTypeDescription));
        } 
    }

    public string ExecutablePath
    {
        get => _executablePath;
        set
        {
            _executablePath = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(LaunchTarget));
            OnPropertyChanged(nameof(HasProjectPath));
            OnPropertyChanged(nameof(IsLaunchable));
            OnPropertyChanged(nameof(ShortProjectPath));
            OnPropertyChanged(nameof(ShortLaunchTarget));
            OnPropertyChanged(nameof(LaunchTypeDescription));
        }
    }

    public string Arguments
    {
        get => _arguments;
        set { _arguments = value; OnPropertyChanged(); }
    }

    public string WorkingDirectory
    {
        get => _workingDirectory;
        set { _workingDirectory = value; OnPropertyChanged(); }
    }

    public bool RunAsAdmin
    {
        get => _runAsAdmin;
        set { _runAsAdmin = value; OnPropertyChanged(); }
    }

    public string LaunchTarget
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(ExecutablePath)) return ExecutablePath;
            return ProjectPath;
        }
    }

    public bool IsDotnetProject
    {
        get
        {
            string target = LaunchTarget;
            return !string.IsNullOrEmpty(target) && 
                   (target.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase) || 
                    target.EndsWith(".sln", StringComparison.OrdinalIgnoreCase) ||
                    target.EndsWith(".fsproj", StringComparison.OrdinalIgnoreCase) ||
                    target.EndsWith(".vbproj", StringComparison.OrdinalIgnoreCase));
        }
    }

    public bool IsUrl
    {
        get
        {
            string target = LaunchTarget.Trim();
            return target.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || 
                   target.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
        }
    }

    public string LaunchTypeDescription
    {
        get
        {
            if (IsUrl) return "Web URL";
            if (IsDotnetProject) return ".NET Project";
            if (LaunchTarget.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) return "Executable";
            if (LaunchTarget.EndsWith(".bat", StringComparison.OrdinalIgnoreCase) || LaunchTarget.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase)) return "Batch Script";
            if (LaunchTarget.EndsWith(".ps1", StringComparison.OrdinalIgnoreCase)) return "PowerShell Script";
            if (!string.IsNullOrEmpty(LaunchTarget)) return "Application / File";
            return "No target configured";
        }
    }

    public bool IsLaunchable => !string.IsNullOrWhiteSpace(LaunchTarget);

    // Keep HasProjectPath for UI backwards compatibility, but true if launchable
    public bool HasProjectPath => IsLaunchable;

    public string ShortProjectPath => ShortLaunchTarget;

    public string ShortLaunchTarget
    {
        get
        {
            string target = LaunchTarget;
            if (string.IsNullOrEmpty(target)) return "No target configured";
            if (IsUrl) return target;
            try
            {
                return Path.GetFileName(target);
            }
            catch
            {
                return target;
            }
        }
    }

    public string ShortcutIndex
    {
        get => _shortcutIndex;
        set { _shortcutIndex = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasShortcut)); }
    }

    public bool HasShortcut => !string.IsNullOrEmpty(ShortcutIndex);

    public bool IsHidden
    {
        get => _isHidden;
        set
        {
            _isHidden = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(Hidden));
        }
    }

    public bool Hidden
    {
        get => _isHidden;
        set => IsHidden = value;
    }

    public string Color 
    { 
        get => _color; 
        set 
        { 
            _color = value; 
            OnPropertyChanged(); 
            OnPropertyChanged(nameof(ColorBrush)); 
        } 
    }

    public string SecondaryColor
    {
        get => _secondaryColor;
        set
        {
            _secondaryColor = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ColorBrush));
        }
    }

    public string BackgroundType
    {
        get => _backgroundType;
        set
        {
            _backgroundType = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ColorBrush));
        }
    }

    public string GradientDirection
    {
        get => _gradientDirection;
        set
        {
            _gradientDirection = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ColorBrush));
        }
    }

    public System.Windows.Media.Brush ColorBrush
    {
        get
        {
            try
            {
                var primaryMediaColor = (System.Windows.Media.Color?)System.Windows.Media.ColorConverter.ConvertFromString(string.IsNullOrWhiteSpace(Color) ? "#1E88E4" : Color);
                if (!primaryMediaColor.HasValue)
                {
                    return new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(0x33, 0xFF, 0xFF, 0xFF));
                }

                bool isGradient = string.Equals(BackgroundType, "Gradient", StringComparison.OrdinalIgnoreCase) ||
                                  (!string.IsNullOrWhiteSpace(SecondaryColor) && !string.Equals(BackgroundType, "Solid", StringComparison.OrdinalIgnoreCase));

                if (isGradient)
                {
                    System.Windows.Media.Color startColor = primaryMediaColor.Value;
                    System.Windows.Media.Color endColor;

                    if (!string.IsNullOrWhiteSpace(SecondaryColor))
                    {
                        var parsed2 = (System.Windows.Media.Color?)System.Windows.Media.ColorConverter.ConvertFromString(SecondaryColor);
                        endColor = parsed2 ?? LerpMedia(primaryMediaColor.Value, System.Windows.Media.Colors.Black, 0.25f);
                    }
                    else
                    {
                        startColor = LerpMedia(primaryMediaColor.Value, System.Windows.Media.Colors.White, 0.35f);
                        endColor = LerpMedia(primaryMediaColor.Value, System.Windows.Media.Colors.Black, 0.25f);
                    }

                    System.Windows.Point startPoint = new System.Windows.Point(0, 0);
                    System.Windows.Point endPoint = new System.Windows.Point(1, 1);
                    string dir = GradientDirection?.Trim().ToLowerInvariant() ?? "diagonal";
                    switch (dir)
                    {
                        case "vertical":
                        case "topbottom":
                        case "toptobottom":
                            startPoint = new System.Windows.Point(0.5, 0);
                            endPoint = new System.Windows.Point(0.5, 1);
                            break;
                        case "horizontal":
                        case "leftright":
                        case "lefttoright":
                            startPoint = new System.Windows.Point(0, 0.5);
                            endPoint = new System.Windows.Point(1, 0.5);
                            break;
                        case "diagonalup":
                        case "bottomlefttotopright":
                            startPoint = new System.Windows.Point(0, 1);
                            endPoint = new System.Windows.Point(1, 0);
                            break;
                        case "diagonal":
                        default:
                            startPoint = new System.Windows.Point(0, 0);
                            endPoint = new System.Windows.Point(1, 1);
                            break;
                    }

                    var gradientBrush = new System.Windows.Media.LinearGradientBrush(startColor, endColor, startPoint, endPoint);
                    if (gradientBrush.CanFreeze) gradientBrush.Freeze();
                    return gradientBrush;
                }
                else
                {
                    var brush = new System.Windows.Media.SolidColorBrush(primaryMediaColor.Value);
                    if (brush.CanFreeze) brush.Freeze();
                    return brush;
                }
            }
            catch
            {
                return new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(0x33, 0xFF, 0xFF, 0xFF));
            }
        }
    }

    private static System.Windows.Media.Color LerpMedia(System.Windows.Media.Color a, System.Windows.Media.Color b, float t)
    {
        byte r = (byte)(a.R + (b.R - a.R) * t);
        byte g = (byte)(a.G + (b.G - a.G) * t);
        byte bl = (byte)(a.B + (b.B - a.B) * t);
        byte alpha = (byte)(a.A + (b.A - a.A) * t);
        return System.Windows.Media.Color.FromArgb(alpha, r, g, bl);
    }

    public string Label 
    { 
        get => _label; 
        set { _label = value; OnPropertyChanged(); } 
    }

    public string FaviconPath
    {
        get => _faviconPath;
        set { _faviconPath = value; OnPropertyChanged(); }
    }

    public string BootstrapIcon
    {
        get => _bootstrapIcon;
        set 
        { 
            _bootstrapIcon = value; 
            OnPropertyChanged(); 
            OnPropertyChanged(nameof(CurrentIconMode));
        }
    }

    public string CustomGlyphSvg
    {
        get => _customGlyphSvg;
        set 
        { 
            _customGlyphSvg = value; 
            OnPropertyChanged(); 
            OnPropertyChanged(nameof(CurrentIconMode));
        }
    }

    public string CustomGlyphColor
    {
        get => _customGlyphColor;
        set { _customGlyphColor = value; OnPropertyChanged(); }
    }

    public string SvgOverride
    {
        get => _svgOverride;
        set 
        { 
            _svgOverride = value; 
            OnPropertyChanged(); 
            OnPropertyChanged(nameof(CurrentIconMode));
        }
    }

    public IconMode CurrentIconMode
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(SvgOverride)) return IconMode.SvgOverride;
            if (!string.IsNullOrWhiteSpace(CustomGlyphSvg)) return IconMode.CustomGlyph;
            if (!string.IsNullOrWhiteSpace(BootstrapIcon)) return IconMode.Bootstrap;
            return IconMode.Label;
        }
    }

    public string IconPath
    {
        get => _iconPath;
        set 
        { 
            _iconPath = value; 
            OnPropertyChanged(); 
            OnPropertyChanged(nameof(IconSource));
            OnPropertyChanged(nameof(IconSource16));
            OnPropertyChanged(nameof(IconSource32));
            OnPropertyChanged(nameof(IconSource48));
        }
    }

    public BitmapSource? PreviewSource
    {
        get => _previewSource;
        set
        {
            _previewSource = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IconSource));
            OnPropertyChanged(nameof(IconSource16));
            OnPropertyChanged(nameof(IconSource32));
            OnPropertyChanged(nameof(IconSource48));
        }
    }

    public BitmapSource? IconSource => PreviewSource ?? LoadIcon(IconPath);

    public BitmapSource? IconSource16 => LoadSpecificSizeIcon(16);
    public BitmapSource? IconSource32 => LoadSpecificSizeIcon(32);
    public BitmapSource? IconSource48 => LoadSpecificSizeIcon(48);

    private BitmapSource? LoadSpecificSizeIcon(int size)
    {
        if (PreviewSource != null) return PreviewSource;
        if (string.IsNullOrEmpty(IconPath)) return null;
        
        string dir = Path.GetDirectoryName(IconPath) ?? string.Empty;
        string specificPath = Path.Combine(dir, $"favicon-{size}x{size}.png");
        
        if (File.Exists(specificPath))
        {
            return LoadIcon(specificPath);
        }
        
        return IconSource;
    }

    private BitmapSource? LoadIcon(string path)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
            return null;

        try
        {
            byte[] bytes = File.ReadAllBytes(path);
            using var stream = new MemoryStream(bytes);
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.StreamSource = stream;
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error loading icon {path}: {ex.Message}");
            return null;
        }
    }

    public void RefreshIcons()
    {
        _previewSource = null;
        OnPropertyChanged(nameof(IconSource));
        OnPropertyChanged(nameof(IconSource16));
        OnPropertyChanged(nameof(IconSource32));
        OnPropertyChanged(nameof(IconSource48));
    }

    public bool IsRunning 
    {
        get
        {
            try
            {
                return Process != null && !Process.HasExited;
            }
            catch
            {
                return Process != null;
            }
        }
    }

    public string IsRunningString => IsRunning ? "Running" : "Stopped";

    public string StatusColor => IsRunning ? "#7EE787" : "#F85149";

    public Process? Process 
    { 
        get => _process; 
        set 
        { 
            _process = value; 
            OnPropertyChanged(); 
            OnPropertyChanged(nameof(IsRunning)); 
            OnPropertyChanged(nameof(IsRunningString)); 
            OnPropertyChanged(nameof(StatusColor)); 
        } 
    }

    public string LogFilePath { get; set; } = string.Empty;

    public List<string> Logs { get; } = new();
    
    public event Action<string>? OnLogReceived;

    public void AddLog(string message)
    {
        lock (Logs)
        {
            Logs.Add(message);
            if (Logs.Count > 1000) Logs.RemoveAt(0);
        }
        OnLogReceived?.Invoke(message);
    }
    
    public void ClearLogs()
    {
        lock (Logs)
        {
            Logs.Clear();
        }
        OnLogReceived?.Invoke(null!);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
