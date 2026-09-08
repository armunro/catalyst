using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Forms;
using System.Windows.Media;
using Catalyst.Adapters.UI.Navigation;
using Catalyst.Adapters.UI.ViewModels;
using Catalyst.Core.Ports.Inbound;
using Microsoft.Extensions.DependencyInjection;
using Wpf.Ui.Controls;

namespace Catalyst.Windows;

public partial class AppManagementWindow : FluentWindow
{
    private readonly AppManagementViewModel _viewModel;
    private bool _isUpdatingConfig = false;
    private System.Threading.Timer? _debounceTimer;
    private System.Windows.Point _dragStartPoint;
    private bool _isDragging = false;

    public AppManagementViewModel ViewModel => _viewModel;

    public string ConfiguredHotkey
    {
        get => _viewModel.ConfiguredHotkey;
        set => _viewModel.ConfiguredHotkey = value;
    }

    public string ConfigFilePath
    {
        get => _viewModel.ConfigFilePath;
        set => _viewModel.ConfigFilePath = value;
    }

    public string RootDir => _viewModel.RootDir;

    public AppManagementWindow() : this(CreateDefaultViewModel())
    {
    }

    private static AppManagementViewModel CreateDefaultViewModel()
    {
        if (App.Services != null)
        {
            return App.Services.GetRequiredService<AppManagementViewModel>();
        }

        var settings = new Adapters.Persistence.JsonSettingsStorage();
        var resolver = new Adapters.Persistence.PathResolver(settings);
        var repo = new Adapters.Persistence.YamlConfigRepository();
        var configService = new Core.Services.AppConfigurationService(repo, settings, resolver);
        var renderer = new Adapters.Icons.SkiaIconRenderer();
        var iconService = new Core.Services.IconManagementService(renderer, configService);
        var hotkeyHook = new Adapters.Platform.WindowsHotkeyHook();
        var hotkeyService = new Core.Services.HotkeyService(hotkeyHook);
        var windowPlacement = new Adapters.Platform.WindowPlacementService();
        var windowService = new WindowService(App.Services!, windowPlacement);

        return new AppManagementViewModel(configService, iconService, hotkeyService, windowService);
    }

    public AppManagementWindow(AppManagementViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;
        LstApps.ItemsSource = new ListCollectionView(_viewModel.Apps);

        if (_viewModel.Apps.Count > 0)
        {
            LstApps.SelectedIndex = 0;
        }
        UpdateReorderButtonStates();
        Closing += AppManagementWindow_Closing;
    }

    public AppManagementWindow(ObservableCollection<AppInfo> apps, string initialHotkey = "Alt+Space", string? initialConfigPath = null)
        : this(CreateDefaultViewModel())
    {
        _viewModel.Initialize(apps, initialHotkey, initialConfigPath);
        LstApps.ItemsSource = new ListCollectionView(_viewModel.Apps);

        if (_viewModel.Apps.Count > 0)
        {
            LstApps.SelectedIndex = 0;
        }
        UpdateReorderButtonStates();
        Closing += AppManagementWindow_Closing;
    }

    private void AppManagementWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        try
        {
            _viewModel.SaveConfig();
        }
        catch { }
    }

    private void UpdateReorderButtonStates()
    {
        if (BtnRemove == null) return;
        int index = LstApps.SelectedIndex;
        BtnRemove.IsEnabled = index >= 0;
    }

    private void LstApps_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateReorderButtonStates();
        if (LstApps.SelectedItem is AppInfo app)
        {
            _isUpdatingConfig = true;
            _viewModel.SelectedApp = app;
            TxtAppName.Text = app.Name;
            TxtProjectPath.Text = app.LaunchTarget;
            TxtArguments.Text = app.Arguments;
            TxtWorkingDirectory.Text = app.WorkingDirectory;
            ChkRunAsAdmin.IsChecked = app.RunAsAdmin;
            ChkIsHidden.IsChecked = app.IsHidden;

            TxtColor.Text = app.Color;
            TxtSecondaryColor.Text = app.SecondaryColor;
            SetComboTag(CmbBackgroundType, string.IsNullOrWhiteSpace(app.BackgroundType) ? (string.IsNullOrWhiteSpace(app.SecondaryColor) ? "Solid" : "Gradient") : app.BackgroundType);
            SetComboTag(CmbGradientDirection, string.IsNullOrWhiteSpace(app.GradientDirection) ? "Diagonal" : app.GradientDirection);

            TxtGlyphColor.Text = app.CustomGlyphColor;
            TxtLabel.Text = app.Label;
            TxtFaviconPath.Text = app.FaviconPath;
            TxtCatalystDirectory.Text = app.CatalystDirectory;
            TxtBootstrapIcon.Text = app.BootstrapIcon;
            TxtCustomGlyphSvg.Text = app.CustomGlyphSvg;
            TxtSvgOverride.Text = app.SvgOverride;
            TxtIconPadding.Text = app.Padding?.ToString() ?? "";

            UpdateTypeBadge(app);
            UpdateColorPreviews();
            UpdateResolvedPathLabels();
            _isUpdatingConfig = false;

            RegeneratePreview(app);
        }
        else
        {
            _isUpdatingConfig = true;
            _viewModel.SelectedApp = null;
            TxtAppName.Text = "";
            TxtProjectPath.Text = "";
            TxtArguments.Text = "";
            TxtWorkingDirectory.Text = "";
            ChkRunAsAdmin.IsChecked = false;
            ChkIsHidden.IsChecked = false;
            TxtColor.Text = "";
            TxtSecondaryColor.Text = "";
            CmbBackgroundType.SelectedIndex = 0;
            CmbGradientDirection.SelectedIndex = 0;
            TxtGlyphColor.Text = "";
            TxtLabel.Text = "";
            TxtFaviconPath.Text = "";
            TxtCatalystDirectory.Text = "";
            TxtBootstrapIcon.Text = "";
            TxtCustomGlyphSvg.Text = "";
            TxtSvgOverride.Text = "";
            TxtIconPadding.Text = "";
            LblTypeBadge.Text = "None Selected";
            UpdateResolvedPathLabels();
            _isUpdatingConfig = false;
        }
    }

    private static void SetComboTag(System.Windows.Controls.ComboBox combo, string tag)
    {
        foreach (var item in combo.Items)
        {
            if (item is ComboBoxItem cbi && string.Equals(cbi.Tag?.ToString(), tag, StringComparison.OrdinalIgnoreCase))
            {
                combo.SelectedItem = cbi;
                return;
            }
        }
        if (combo.Items.Count > 0)
        {
            combo.SelectedIndex = 0;
        }
    }

    private void UpdateTypeBadge(AppInfo app)
    {
        LblTypeBadge.Text = app.LaunchTypeDescription;
    }

    private void UpdateColorPreviews()
    {
        try
        {
            string hex = TxtColor.Text?.Trim() ?? "";
            if (!string.IsNullOrEmpty(hex))
            {
                var brush = (System.Windows.Media.Brush?)new System.Windows.Media.BrushConverter().ConvertFromString(hex);
                if (brush != null) ColorPreviewBorder.Background = brush;
            }
        }
        catch { }

        try
        {
            string secHex = TxtSecondaryColor.Text?.Trim() ?? "";
            if (!string.IsNullOrEmpty(secHex))
            {
                var brush = (System.Windows.Media.Brush?)new System.Windows.Media.BrushConverter().ConvertFromString(secHex);
                if (brush != null) SecondaryColorPreviewBorder.Background = brush;
            }
        }
        catch { }

        try
        {
            string glyphHex = TxtGlyphColor.Text?.Trim() ?? "";
            if (!string.IsNullOrEmpty(glyphHex))
            {
                var brush = (System.Windows.Media.Brush?)new System.Windows.Media.BrushConverter().ConvertFromString(glyphHex);
                if (brush != null) GlyphColorPreviewBorder.Background = brush;
            }
        }
        catch { }
    }

    private void Config_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isUpdatingConfig || LstApps.SelectedItem is not AppInfo app) return;

        if (sender == TxtAppName) app.Name = TxtAppName.Text;
        else if (sender == TxtProjectPath)
        {
            string path = TxtProjectPath.Text;
            if (path.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".sln", StringComparison.OrdinalIgnoreCase))
            {
                app.ProjectPath = path;
                app.ExecutablePath = string.Empty;
            }
            else
            {
                app.ExecutablePath = path;
            }
            UpdateTypeBadge(app);
        }
        else if (sender == TxtArguments) app.Arguments = TxtArguments.Text;
        else if (sender == TxtWorkingDirectory) app.WorkingDirectory = TxtWorkingDirectory.Text;
        else if (sender == TxtColor) { app.Color = TxtColor.Text; UpdateColorPreviews(); }
        else if (sender == TxtSecondaryColor)
        {
            app.SecondaryColor = TxtSecondaryColor.Text;
            if (!string.IsNullOrWhiteSpace(app.SecondaryColor) && (string.IsNullOrWhiteSpace(app.BackgroundType) || app.BackgroundType == "Solid"))
            {
                app.BackgroundType = "Gradient";
                SetComboTag(CmbBackgroundType, "Gradient");
            }
            UpdateColorPreviews();
        }
        else if (sender == TxtGlyphColor) { app.CustomGlyphColor = TxtGlyphColor.Text; UpdateColorPreviews(); }
        else if (sender == TxtLabel) app.Label = TxtLabel.Text;
        else if (sender == TxtFaviconPath) app.FaviconPath = TxtFaviconPath.Text;
        else if (sender == TxtCatalystDirectory) app.CatalystDirectory = TxtCatalystDirectory.Text;
        else if (sender == TxtBootstrapIcon) app.BootstrapIcon = TxtBootstrapIcon.Text;
        else if (sender == TxtCustomGlyphSvg) app.CustomGlyphSvg = TxtCustomGlyphSvg.Text;
        else if (sender == TxtSvgOverride) app.SvgOverride = TxtSvgOverride.Text;
        else if (sender == TxtIconPadding)
        {
            string padText = TxtIconPadding.Text?.Trim() ?? "";
            if (string.IsNullOrEmpty(padText))
            {
                app.Padding = null;
            }
            else
            {
                padText = padText.TrimEnd('%').Trim();
                if (int.TryParse(padText, out int padVal))
                {
                    app.Padding = Math.Clamp(padVal, 0, 49);
                }
            }
        }

        UpdateResolvedPathLabels();

        // Debounce icon regeneration preview
        _debounceTimer?.Dispose();
        _debounceTimer = new System.Threading.Timer(DebouncedRegeneratePreview, app, 300, System.Threading.Timeout.Infinite);
    }

    private void UpdateResolvedPathLabels()
    {
        if (LblResolvedProjectPath == null ||
            LblResolvedWorkingDirectory == null ||
            LblResolvedCustomGlyphSvg == null ||
            LblResolvedSvgOverride == null ||
            LblResolvedCatalystDirectory == null ||
            LblResolvedFaviconPath == null)
        {
            return;
        }

        if (LstApps.SelectedItem is not AppInfo)
        {
            LblResolvedProjectPath.Text = "Full Path: ";
            LblResolvedProjectPath.ToolTip = null;
            LblResolvedWorkingDirectory.Text = "Full Path: ";
            LblResolvedWorkingDirectory.ToolTip = null;
            LblResolvedCustomGlyphSvg.Text = "Full Path: ";
            LblResolvedCustomGlyphSvg.ToolTip = null;
            LblResolvedSvgOverride.Text = "Full Path: ";
            LblResolvedSvgOverride.ToolTip = null;
            LblResolvedCatalystDirectory.Text = "Full Path: ";
            LblResolvedCatalystDirectory.ToolTip = null;
            LblResolvedFaviconPath.Text = "Full Path: ";
            LblResolvedFaviconPath.ToolTip = null;
            return;
        }

        string rootDir = RootDir;
        string targetText = TxtProjectPath.Text?.Trim() ?? string.Empty;
        string workDirText = TxtWorkingDirectory.Text?.Trim() ?? string.Empty;

        string projPath = targetText.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase) ||
                          targetText.EndsWith(".sln", StringComparison.OrdinalIgnoreCase) ||
                          targetText.EndsWith(".fsproj", StringComparison.OrdinalIgnoreCase) ||
                          targetText.EndsWith(".vbproj", StringComparison.OrdinalIgnoreCase)
            ? targetText
            : string.Empty;
        string execPath = string.IsNullOrEmpty(projPath) ? targetText : string.Empty;

        string? projectDir = Catalyst.Core.Services.AppConfigurationService.GetProjectDirectory(projPath, workDirText, rootDir, execPath);

        // 1. Launch Target
        if (string.IsNullOrWhiteSpace(targetText))
        {
            LblResolvedProjectPath.Text = "Full Path: (None configured)";
            LblResolvedProjectPath.ToolTip = null;
        }
        else if (targetText.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || targetText.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            LblResolvedProjectPath.Text = $"Target URL: {targetText}";
            LblResolvedProjectPath.ToolTip = targetText;
        }
        else
        {
            string fullTarget = Catalyst.Core.Services.AppConfigurationService.ResolveExpectedFullPath(targetText, null, rootDir);
            LblResolvedProjectPath.Text = $"Full Path: {fullTarget}";
            LblResolvedProjectPath.ToolTip = fullTarget;
        }

        // 2. Working Directory
        if (string.IsNullOrWhiteSpace(workDirText))
        {
            string defaultDir = !string.IsNullOrEmpty(projectDir) ? projectDir : (!string.IsNullOrEmpty(rootDir) ? rootDir : string.Empty);
            LblResolvedWorkingDirectory.Text = !string.IsNullOrEmpty(defaultDir) ? $"Full Path (Default): {defaultDir}" : "Full Path: (Default)";
            LblResolvedWorkingDirectory.ToolTip = defaultDir;
        }
        else
        {
            string fullWorkDir = Catalyst.Core.Services.AppConfigurationService.ResolveExpectedFullPath(workDirText, null, rootDir);
            LblResolvedWorkingDirectory.Text = $"Full Path: {fullWorkDir}";
            LblResolvedWorkingDirectory.ToolTip = fullWorkDir;
        }

        // 3. Custom Glyph SVG
        string glyphText = TxtCustomGlyphSvg.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(glyphText))
        {
            LblResolvedCustomGlyphSvg.Text = "Full Path: (Not configured)";
            LblResolvedCustomGlyphSvg.ToolTip = null;
        }
        else if (glyphText.StartsWith("<"))
        {
            LblResolvedCustomGlyphSvg.Text = "Source: Inline SVG XML";
            LblResolvedCustomGlyphSvg.ToolTip = "Inline SVG XML";
        }
        else
        {
            string fullGlyph = Catalyst.Core.Services.AppConfigurationService.ResolveExpectedFullPath(glyphText, projectDir, rootDir);
            LblResolvedCustomGlyphSvg.Text = $"Full Path: {fullGlyph}";
            LblResolvedCustomGlyphSvg.ToolTip = fullGlyph;
        }

        // 4. Complete SVG Override
        string overrideText = TxtSvgOverride.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(overrideText))
        {
            LblResolvedSvgOverride.Text = "Full Path: (Not configured)";
            LblResolvedSvgOverride.ToolTip = null;
        }
        else if (overrideText.StartsWith("<"))
        {
            LblResolvedSvgOverride.Text = "Source: Inline SVG XML";
            LblResolvedSvgOverride.ToolTip = "Inline SVG XML";
        }
        else
        {
            string fullOverride = Catalyst.Core.Services.AppConfigurationService.ResolveExpectedFullPath(overrideText, projectDir, rootDir);
            LblResolvedSvgOverride.Text = $"Full Path: {fullOverride}";
            LblResolvedSvgOverride.ToolTip = fullOverride;
        }

        // 5. Catalyst Directory
        string catalystText = TxtCatalystDirectory.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(catalystText))
        {
            string defaultCatDir = Catalyst.Core.Services.AppConfigurationService.ResolveExpectedFullPath(string.Empty, projectDir, rootDir, ".catalyst");
            LblResolvedCatalystDirectory.Text = $"Full Path (Default): {defaultCatDir}";
            LblResolvedCatalystDirectory.ToolTip = defaultCatDir;
        }
        else
        {
            string fullCatDir = Catalyst.Core.Services.AppConfigurationService.ResolveExpectedFullPath(catalystText, projectDir, rootDir);
            LblResolvedCatalystDirectory.Text = $"Full Path: {fullCatDir}";
            LblResolvedCatalystDirectory.ToolTip = fullCatDir;
        }

        // 6. Project Favicon Path
        string faviconText = TxtFaviconPath.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(faviconText))
        {
            string defaultFav = Catalyst.Core.Services.AppConfigurationService.ResolveExpectedFullPath(string.Empty, projectDir, rootDir, "favicon.ico");
            LblResolvedFaviconPath.Text = $"Full Path (Default): {defaultFav}";
            LblResolvedFaviconPath.ToolTip = defaultFav;
        }
        else
        {
            string fullFav = Catalyst.Core.Services.AppConfigurationService.ResolveExpectedFullPath(faviconText, projectDir, rootDir);
            LblResolvedFaviconPath.Text = $"Full Path: {fullFav}";
            LblResolvedFaviconPath.ToolTip = fullFav;
        }
    }

    private void Config_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingConfig || LstApps.SelectedItem is not AppInfo app) return;

        if (sender == CmbBackgroundType && CmbBackgroundType.SelectedItem is ComboBoxItem bgItem && bgItem.Tag is string bgTag)
        {
            app.BackgroundType = bgTag;
        }
        else if (sender == CmbGradientDirection && CmbGradientDirection.SelectedItem is ComboBoxItem dirItem && dirItem.Tag is string dirTag)
        {
            app.GradientDirection = dirTag;
        }

        _debounceTimer?.Dispose();
        _debounceTimer = new System.Threading.Timer(DebouncedRegeneratePreview, app, 300, System.Threading.Timeout.Infinite);
    }

    private void Config_CheckChanged(object sender, RoutedEventArgs e)
    {
        if (_isUpdatingConfig || LstApps.SelectedItem is not AppInfo app) return;
        app.RunAsAdmin = ChkRunAsAdmin.IsChecked == true;
        app.IsHidden = ChkIsHidden.IsChecked == true;
    }

    private void DebouncedRegeneratePreview(object? state)
    {
        if (state is AppInfo app)
        {
            Dispatcher.Invoke(() => RegeneratePreview(app));
        }
    }

    private void BtnPickColor_Click(object sender, RoutedEventArgs e)
    {
        string currentHex = TxtColor.Text?.Trim() ?? "#1E88E4";
        string? chosen = ColorPickerWindow.ShowDialog(this, currentHex, hex =>
        {
            TxtColor.Text = hex;
            if (LstApps.SelectedItem is AppInfo app)
            {
                app.Color = hex;
                UpdateColorPreviews();
                RegeneratePreview(app);
            }
        });

        if (chosen != null)
        {
            TxtColor.Text = chosen;
            if (LstApps.SelectedItem is AppInfo app)
            {
                app.Color = chosen;
                UpdateColorPreviews();
                RegeneratePreview(app);
            }
        }
    }

    private void BtnPickSecondaryColor_Click(object sender, RoutedEventArgs e)
    {
        string currentHex = TxtSecondaryColor.Text?.Trim() ?? "#DD2476";
        string? chosen = ColorPickerWindow.ShowDialog(this, currentHex, hex =>
        {
            TxtSecondaryColor.Text = hex;
            if (LstApps.SelectedItem is AppInfo app)
            {
                app.SecondaryColor = hex;
                if (!string.IsNullOrWhiteSpace(app.SecondaryColor) && (string.IsNullOrWhiteSpace(app.BackgroundType) || app.BackgroundType == "Solid"))
                {
                    app.BackgroundType = "Gradient";
                    SetComboTag(CmbBackgroundType, "Gradient");
                }
                UpdateColorPreviews();
                RegeneratePreview(app);
            }
        });

        if (chosen != null)
        {
            TxtSecondaryColor.Text = chosen;
            if (LstApps.SelectedItem is AppInfo app)
            {
                app.SecondaryColor = chosen;
                if (!string.IsNullOrWhiteSpace(app.SecondaryColor) && (string.IsNullOrWhiteSpace(app.BackgroundType) || app.BackgroundType == "Solid"))
                {
                    app.BackgroundType = "Gradient";
                    SetComboTag(CmbBackgroundType, "Gradient");
                }
                UpdateColorPreviews();
                RegeneratePreview(app);
            }
        }
    }

    private void BtnPickGlyphColor_Click(object sender, RoutedEventArgs e)
    {
        string currentHex = TxtGlyphColor.Text?.Trim() ?? "#FFFFFF";
        string? chosen = ColorPickerWindow.ShowDialog(this, currentHex, hex =>
        {
            TxtGlyphColor.Text = hex;
            if (LstApps.SelectedItem is AppInfo app)
            {
                app.CustomGlyphColor = hex;
                UpdateColorPreviews();
                RegeneratePreview(app);
            }
        });

        if (chosen != null)
        {
            TxtGlyphColor.Text = chosen;
            if (LstApps.SelectedItem is AppInfo app)
            {
                app.CustomGlyphColor = chosen;
                UpdateColorPreviews();
                RegeneratePreview(app);
            }
        }
    }

    private void RegeneratePreview(AppInfo app)
    {
        try
        {
            app.LivePreviewSource = _viewModel.RenderPreview(app, 512);
            app.LivePreviewSource48 = _viewModel.RenderPreview(app, 48);
            app.LivePreviewSource32 = _viewModel.RenderPreview(app, 32);
            app.LivePreviewSource16 = _viewModel.RenderPreview(app, 16);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error generating preview: {ex.Message}");
        }
    }

    private void BtnBrowseTarget_Click(object sender, RoutedEventArgs e)
    {
        if (LstApps.SelectedItem is not AppInfo app) return;

        string rootDir = RootDir;

        using var dialog = new System.Windows.Forms.OpenFileDialog
        {
            Title = "Select Application / Project / Executable",
            InitialDirectory = rootDir,
            Filter = "All Supported (*.exe;*.csproj;*.sln;*.bat;*.cmd;*.ps1;*.lnk)|*.exe;*.csproj;*.sln;*.bat;*.cmd;*.ps1;*.lnk|Executables (*.exe;*.bat;*.cmd;*.ps1)|*.exe;*.bat;*.cmd;*.ps1|.NET Projects (*.csproj;*.sln)|*.csproj;*.sln|All Files (*.*)|*.*"
        };

        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            string path = dialog.FileName;
            if (path.StartsWith(rootDir, StringComparison.OrdinalIgnoreCase))
            {
                path = Path.GetRelativePath(rootDir, path);
            }

            TxtProjectPath.Text = path;

            if (string.IsNullOrWhiteSpace(TxtAppName.Text) || TxtAppName.Text == "NewApp")
            {
                TxtAppName.Text = Path.GetFileNameWithoutExtension(dialog.FileName);
            }
        }
    }

    private void BtnBrowseWorkingDir_Click(object sender, RoutedEventArgs e)
    {
        if (LstApps.SelectedItem is not AppInfo app) return;

        string rootDir = RootDir;

        using var dialog = new FolderBrowserDialog
        {
            Description = "Select Working Directory",
            SelectedPath = rootDir,
            UseDescriptionForTitle = true
        };

        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            string path = dialog.SelectedPath;
            if (path.StartsWith(rootDir, StringComparison.OrdinalIgnoreCase))
            {
                path = Path.GetRelativePath(rootDir, path);
            }
            TxtWorkingDirectory.Text = path;
        }
    }

    private void BtnBrowseGlyphSvg_Click(object sender, RoutedEventArgs e)
    {
        if (LstApps.SelectedItem is not AppInfo app) return;

        string rootDir = RootDir;
        string? projectDir = Catalyst.Core.Services.AppConfigurationService.GetProjectDirectory(app.ProjectPath, app.WorkingDirectory, rootDir, app.ExecutablePath);
        string initialDir = !string.IsNullOrEmpty(projectDir) && Directory.Exists(Path.Combine(projectDir, "icons"))
            ? Path.Combine(projectDir, "icons")
            : (!string.IsNullOrEmpty(projectDir) && Directory.Exists(projectDir) ? projectDir : Path.Combine(rootDir, "icons"));

        using var dialog = new System.Windows.Forms.OpenFileDialog
        {
            Title = "Select Custom Glyph SVG File",
            InitialDirectory = initialDir,
            Filter = "SVG Files (*.svg)|*.svg|All Files (*.*)|*.*"
        };

        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            string path = dialog.FileName;
            if (!string.IsNullOrEmpty(projectDir) && Catalyst.Core.Services.AppConfigurationService.IsSubPathOf(path, projectDir))
            {
                path = Path.GetRelativePath(projectDir, path);
            }
            else if (Catalyst.Core.Services.AppConfigurationService.IsSubPathOf(path, rootDir))
            {
                path = Path.GetRelativePath(rootDir, path);
            }
            TxtCustomGlyphSvg.Text = path;
        }
    }

    private void BtnBrowseSvgOverride_Click(object sender, RoutedEventArgs e)
    {
        if (LstApps.SelectedItem is not AppInfo app) return;

        string rootDir = RootDir;
        string? projectDir = Catalyst.Core.Services.AppConfigurationService.GetProjectDirectory(app.ProjectPath, app.WorkingDirectory, rootDir, app.ExecutablePath);
        string initialDir = !string.IsNullOrEmpty(projectDir) && Directory.Exists(Path.Combine(projectDir, "icons"))
            ? Path.Combine(projectDir, "icons")
            : (!string.IsNullOrEmpty(projectDir) && Directory.Exists(projectDir) ? projectDir : Path.Combine(rootDir, "icons"));

        using var dialog = new System.Windows.Forms.OpenFileDialog
        {
            Title = "Select Complete SVG Override File",
            InitialDirectory = initialDir,
            Filter = "SVG Files (*.svg)|*.svg|All Files (*.*)|*.*"
        };

        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            string path = dialog.FileName;
            if (!string.IsNullOrEmpty(projectDir) && Catalyst.Core.Services.AppConfigurationService.IsSubPathOf(path, projectDir))
            {
                path = Path.GetRelativePath(projectDir, path);
            }
            else if (Catalyst.Core.Services.AppConfigurationService.IsSubPathOf(path, rootDir))
            {
                path = Path.GetRelativePath(rootDir, path);
            }
            TxtSvgOverride.Text = path;
        }
    }

    private void BtnBrowseFavicon_Click(object sender, RoutedEventArgs e)
    {
        if (LstApps.SelectedItem is not AppInfo app) return;

        string rootDir = RootDir;
        string? projectDir = Catalyst.Core.Services.AppConfigurationService.GetProjectDirectory(app.ProjectPath, app.WorkingDirectory, rootDir, app.ExecutablePath);
        string initialDir = !string.IsNullOrEmpty(projectDir) && Directory.Exists(projectDir) ? projectDir : rootDir;

        using var dialog = new System.Windows.Forms.OpenFileDialog
        {
            Title = "Select Project Favicon Target",
            InitialDirectory = initialDir,
            Filter = "Icon / Vector Files (*.ico;*.png;*.svg)|*.ico;*.png;*.svg|ICO Files (*.ico)|*.ico|PNG Files (*.png)|*.png|SVG Files (*.svg)|*.svg|All Files (*.*)|*.*"
        };

        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            string path = dialog.FileName;
            if (!string.IsNullOrEmpty(projectDir) && Catalyst.Core.Services.AppConfigurationService.IsSubPathOf(path, projectDir))
            {
                path = Path.GetRelativePath(projectDir, path);
            }
            else if (Catalyst.Core.Services.AppConfigurationService.IsSubPathOf(path, rootDir))
            {
                path = Path.GetRelativePath(rootDir, path);
            }
            TxtFaviconPath.Text = path;
        }
    }

    private void BtnBrowseCatalystDir_Click(object sender, RoutedEventArgs e)
    {
        if (LstApps.SelectedItem is not AppInfo app) return;

        string rootDir = RootDir;
        string? projectDir = Catalyst.Core.Services.AppConfigurationService.GetProjectDirectory(app.ProjectPath, app.WorkingDirectory, rootDir, app.ExecutablePath);
        string baseDir = !string.IsNullOrEmpty(projectDir) && Directory.Exists(projectDir) ? projectDir : rootDir;

        string initialDir = !string.IsNullOrWhiteSpace(app.CatalystDirectory)
            ? (Path.IsPathRooted(app.CatalystDirectory)
                ? app.CatalystDirectory
                : Path.GetFullPath(Path.Combine(baseDir, app.CatalystDirectory)))
            : baseDir;

        if (!Directory.Exists(initialDir))
        {
            initialDir = baseDir;
        }

        using var dialog = new FolderBrowserDialog
        {
            Description = "Select .catalyst Directory or Project Directory",
            SelectedPath = initialDir,
            UseDescriptionForTitle = true
        };

        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            string path = dialog.SelectedPath;
            if (!string.IsNullOrEmpty(projectDir) && Catalyst.Core.Services.AppConfigurationService.IsSubPathOf(path, projectDir))
            {
                path = Path.GetRelativePath(projectDir, path);
            }
            else if (Catalyst.Core.Services.AppConfigurationService.IsSubPathOf(path, rootDir))
            {
                path = Path.GetRelativePath(rootDir, path);
            }
            TxtCatalystDirectory.Text = path;
        }
    }

    private void BtnRegenerateCurrent_Click(object sender, RoutedEventArgs e)
    {
        if (LstApps.SelectedItem is not AppInfo app) return;

        try
        {
            _viewModel.SaveConfig();

            _viewModel.GenerateIcon(app, (msg) => Debug.WriteLine(msg));

            string iconPath = Path.Combine(_viewModel.IconsBaseDir, app.Name, $"{app.Name}.png");
            if (File.Exists(iconPath))
            {
                app.IconPath = iconPath;
            }

            app.RefreshIcons();
            RegeneratePreview(app);
            _viewModel.SaveConfig();

            LblGenesisStatus.Text = $"Icon saved for {app.Name}";
            LblGenesisStatus.Foreground = System.Windows.Media.Brushes.Green;
        }
        catch (Exception ex)
        {
            LblGenesisStatus.Text = "Error";
            LblGenesisStatus.Foreground = System.Windows.Media.Brushes.Red;
            System.Windows.MessageBox.Show($"Error generating icon: {ex.Message}");
        }
    }

    private void BtnAdd_Click(object sender, RoutedEventArgs e)
    {
        var newApp = _viewModel.AddNewApp();
        LstApps.SelectedItem = newApp;
        UpdateReorderButtonStates();
    }

    private void BtnRemove_Click(object sender, RoutedEventArgs e)
    {
        if (LstApps.SelectedItem is AppInfo app)
        {
            _viewModel.DeleteApp(app);
            UpdateReorderButtonStates();
        }
    }

    private void MoveSelectedAppUp()
    {
        int index = LstApps.SelectedIndex;
        if (index > 0)
        {
            var app = _viewModel.Apps[index];
            _viewModel.MoveAppUp(app);
            LstApps.SelectedItem = app;
            LstApps.ScrollIntoView(app);
            UpdateReorderButtonStates();
        }
    }

    private void MoveSelectedAppDown()
    {
        int index = LstApps.SelectedIndex;
        if (index >= 0 && index < _viewModel.Apps.Count - 1)
        {
            var app = _viewModel.Apps[index];
            _viewModel.MoveAppDown(app);
            LstApps.SelectedItem = app;
            LstApps.ScrollIntoView(app);
            UpdateReorderButtonStates();
        }
    }

    private void LstApps_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        bool isAlt = (System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Alt) == System.Windows.Input.ModifierKeys.Alt;
        bool isCtrl = (System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Control) == System.Windows.Input.ModifierKeys.Control;

        if ((isAlt || isCtrl) && e.Key == System.Windows.Input.Key.Up)
        {
            MoveSelectedAppUp();
            e.Handled = true;
        }
        else if ((isAlt || isCtrl) && e.Key == System.Windows.Input.Key.Down)
        {
            MoveSelectedAppDown();
            e.Handled = true;
        }
    }

    private void LstApps_PreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        _dragStartPoint = e.GetPosition(null);
    }

    private void LstApps_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (e.LeftButton == System.Windows.Input.MouseButtonState.Pressed && !_isDragging)
        {
            System.Windows.Point position = e.GetPosition(null);
            if (Math.Abs(position.X - _dragStartPoint.X) > SystemParameters.MinimumHorizontalDragDistance ||
                Math.Abs(position.Y - _dragStartPoint.Y) > SystemParameters.MinimumVerticalDragDistance)
            {
                if (LstApps.SelectedItem is AppInfo selectedApp)
                {
                    _isDragging = true;
                    try
                    {
                        System.Windows.DragDrop.DoDragDrop(LstApps, selectedApp, System.Windows.DragDropEffects.Move);
                    }
                    finally
                    {
                        _isDragging = false;
                    }
                }
            }
        }
    }

    private void LstApps_DragOver(object sender, System.Windows.DragEventArgs e)
    {
        if (e.Data.GetDataPresent(typeof(AppInfo)))
        {
            e.Effects = System.Windows.DragDropEffects.Move;
            e.Handled = true;
        }
        else
        {
            e.Effects = System.Windows.DragDropEffects.None;
        }
    }

    private void LstApps_Drop(object sender, System.Windows.DragEventArgs e)
    {
        if (e.Data.GetData(typeof(AppInfo)) is AppInfo sourceApp)
        {
            int oldIndex = _viewModel.Apps.IndexOf(sourceApp);
            if (oldIndex < 0) return;

            var targetApp = GetAppInfoUnderMouse(e.GetPosition(LstApps));
            int newIndex;
            if (targetApp != null)
            {
                newIndex = _viewModel.Apps.IndexOf(targetApp);
                if (newIndex < 0) newIndex = _viewModel.Apps.Count - 1;
            }
            else
            {
                newIndex = _viewModel.Apps.Count - 1;
            }

            if (oldIndex != newIndex)
            {
                _viewModel.MoveApp(oldIndex, newIndex);
                LstApps.SelectedItem = sourceApp;
                LstApps.ScrollIntoView(sourceApp);
                UpdateReorderButtonStates();
            }
        }
    }

    private AppInfo? GetAppInfoUnderMouse(System.Windows.Point position)
    {
        HitTestResult hitResult = VisualTreeHelper.HitTest(LstApps, position);
        if (hitResult?.VisualHit != null)
        {
            DependencyObject? current = hitResult.VisualHit;
            while (current != null && current != LstApps)
            {
                if (current is ListBoxItem lbi && lbi.DataContext is AppInfo app)
                {
                    return app;
                }
                current = VisualTreeHelper.GetParent(current);
            }
        }
        return null;
    }

    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        string targetConfigPath = _viewModel.ConfigFilePath;

        try
        {
            _viewModel.SaveConfig();

            LblGenesisStatus.Text = $"Saved to {Path.GetFileName(targetConfigPath)}";
            LblGenesisStatus.Foreground = System.Windows.Media.Brushes.Green;
            System.Windows.MessageBox.Show($"Configuration successfully saved to {targetConfigPath}", "Catalyst", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            LblGenesisStatus.Text = "Error saving";
            LblGenesisStatus.Foreground = System.Windows.Media.Brushes.Red;
            System.Windows.MessageBox.Show($"Error saving config: {ex.Message}");
        }
    }

    private async void BtnGenerate_Click(object sender, RoutedEventArgs e)
    {
        BtnGenerate.IsEnabled = false;
        if (BtnUpdateCurrentFavicon != null) BtnUpdateCurrentFavicon.IsEnabled = false;
        LblGenesisStatus.Text = "Generating icons...";
        LblGenesisStatus.Foreground = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#58A6FF"));

        try
        {
            _viewModel.SaveConfig();

            string iconsBaseDir = _viewModel.IconsBaseDir;

            await Task.Run(() =>
            {
                _viewModel.GenerateAllIcons((msg) => Debug.WriteLine(msg));
            });

            foreach (var app in _viewModel.Apps)
            {
                string iconPath = Path.Combine(iconsBaseDir, app.Name, $"{app.Name}.png");
                if (File.Exists(iconPath))
                {
                    app.IconPath = iconPath;
                }
                app.RefreshIcons();
            }

            _viewModel.SaveConfig();

            if (LstApps.SelectedItem is AppInfo selectedApp)
            {
                RegeneratePreview(selectedApp);
            }

            LblGenesisStatus.Text = "Completed";
            LblGenesisStatus.Foreground = System.Windows.Media.Brushes.Green;

            _viewModel.ShowIconsResult(this);
        }
        catch (Exception ex)
        {
            LblGenesisStatus.Text = "Error";
            LblGenesisStatus.Foreground = System.Windows.Media.Brushes.Red;
            System.Windows.MessageBox.Show($"Error during generation: {ex.Message}");
        }
        finally
        {
            BtnGenerate.IsEnabled = true;
            if (BtnUpdateCurrentFavicon != null) BtnUpdateCurrentFavicon.IsEnabled = true;
        }
    }

    private async void BtnUpdateCurrentFavicon_Click(object sender, RoutedEventArgs e)
    {
        if (LstApps.SelectedItem is not AppInfo app) return;

        if (string.IsNullOrWhiteSpace(app.FaviconPath))
        {
            System.Windows.MessageBox.Show("Please configure a Project Favicon Path for this app first.", "Catalyst");
            return;
        }

        if (BtnUpdateCurrentFavicon != null) BtnUpdateCurrentFavicon.IsEnabled = false;
        BtnGenerate.IsEnabled = false;
        LblGenesisStatus.Text = $"Updating favicon for {app.Name}...";
        LblGenesisStatus.Foreground = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#58A6FF"));

        try
        {
            _viewModel.SaveConfig();

            bool success = false;
            await Task.Run(() =>
            {
                success = _viewModel.UpdateProjectFavicon(app, (msg) => Debug.WriteLine(msg));
            });

            if (success)
            {
                LblGenesisStatus.Text = $"Updated favicon for {app.Name}";
                LblGenesisStatus.Foreground = System.Windows.Media.Brushes.Green;
                System.Windows.MessageBox.Show($"Successfully updated project favicon for '{app.Name}'.", "Catalyst");
            }
            else
            {
                LblGenesisStatus.Text = "Failed to update favicon";
                LblGenesisStatus.Foreground = System.Windows.Media.Brushes.OrangeRed;
                System.Windows.MessageBox.Show($"Could not update favicon for '{app.Name}'. Ensure the icon is generated and the target path is valid.", "Catalyst");
            }
        }
        catch (Exception ex)
        {
            LblGenesisStatus.Text = "Error";
            LblGenesisStatus.Foreground = System.Windows.Media.Brushes.Red;
            System.Windows.MessageBox.Show($"Error updating favicon: {ex.Message}", "Catalyst");
        }
        finally
        {
            if (BtnUpdateCurrentFavicon != null) BtnUpdateCurrentFavicon.IsEnabled = true;
            BtnGenerate.IsEnabled = true;
        }
    }

    private async void BtnExportCatalyst_Click(object sender, RoutedEventArgs e)
    {
        if (LstApps.SelectedItem is not AppInfo app) return;

        if (BtnExportCatalyst != null) BtnExportCatalyst.IsEnabled = false;
        BtnGenerate.IsEnabled = false;
        LblGenesisStatus.Text = $"Exporting .catalyst pack for {app.Name}...";
        LblGenesisStatus.Foreground = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#58A6FF"));

        try
        {
            _viewModel.SaveConfig();

            string exportedPath = string.Empty;
            Action<string> logger = message => Debug.WriteLine(message);
            await Task.Run(() =>
            {
                exportedPath = _viewModel.ExportCatalystFolder(app, null, logger);
            });

            if (!string.IsNullOrEmpty(exportedPath))
            {
                _viewModel.SaveConfig();
                TxtCatalystDirectory.Text = app.CatalystDirectory ?? string.Empty;

                LblGenesisStatus.Text = $"Exported .catalyst for {app.Name}";
                LblGenesisStatus.Foreground = System.Windows.Media.Brushes.Green;
                System.Windows.MessageBox.Show($"Successfully exported .catalyst folder for '{app.Name}' to:\n{exportedPath}", "Catalyst");
            }
        }
        catch (Exception ex)
        {
            LblGenesisStatus.Text = "Error exporting";
            LblGenesisStatus.Foreground = System.Windows.Media.Brushes.Red;
            System.Windows.MessageBox.Show($"Error exporting .catalyst folder: {ex.Message}", "Catalyst");
        }
        finally
        {
            if (BtnExportCatalyst != null) BtnExportCatalyst.IsEnabled = true;
            BtnGenerate.IsEnabled = true;
        }
    }
}
