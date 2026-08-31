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

    public AppManagementWindow() : this(
        App.Services != null ? App.Services.GetRequiredService<AppManagementViewModel>() : new AppManagementViewModel(
            new Core.Services.AppConfigurationService(new Adapters.Persistence.YamlConfigRepository(), new Adapters.Persistence.JsonSettingsStorage(), new Adapters.Persistence.PathResolver(new Adapters.Persistence.JsonSettingsStorage())),
            new Core.Services.IconManagementService(new Adapters.Icons.SkiaIconRenderer(), new Core.Services.AppConfigurationService(new Adapters.Persistence.YamlConfigRepository(), new Adapters.Persistence.JsonSettingsStorage(), new Adapters.Persistence.PathResolver(new Adapters.Persistence.JsonSettingsStorage()))),
            new Core.Services.HotkeyService(new Adapters.Platform.WindowsHotkeyHook()),
            new WindowService(App.Services!, new Adapters.Platform.WindowPlacementService())))
    {
    }

    public AppManagementWindow(AppManagementViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;
        LstApps.ItemsSource = new ListCollectionView(_viewModel.Apps);

        TxtGlobalHotkey.Text = _viewModel.ConfiguredHotkey;
        TxtConfigPath.Text = _viewModel.ConfigFilePath;

        if (_viewModel.Apps.Count > 0)
        {
            LstApps.SelectedIndex = 0;
        }
        UpdateReorderButtonStates();
    }

    public AppManagementWindow(ObservableCollection<AppInfo> apps, string initialHotkey = "Alt+Space", string? initialConfigPath = null)
        : this(App.Services != null ? App.Services.GetRequiredService<AppManagementViewModel>() : new AppManagementViewModel(
            new Core.Services.AppConfigurationService(new Adapters.Persistence.YamlConfigRepository(), new Adapters.Persistence.JsonSettingsStorage(), new Adapters.Persistence.PathResolver(new Adapters.Persistence.JsonSettingsStorage())),
            new Core.Services.IconManagementService(new Adapters.Icons.SkiaIconRenderer(), new Core.Services.AppConfigurationService(new Adapters.Persistence.YamlConfigRepository(), new Adapters.Persistence.JsonSettingsStorage(), new Adapters.Persistence.PathResolver(new Adapters.Persistence.JsonSettingsStorage()))),
            new Core.Services.HotkeyService(new Adapters.Platform.WindowsHotkeyHook()),
            new WindowService(App.Services!, new Adapters.Platform.WindowPlacementService())))
    {
        _viewModel.Initialize(apps, initialHotkey, initialConfigPath);
        LstApps.ItemsSource = new ListCollectionView(_viewModel.Apps);
        TxtGlobalHotkey.Text = _viewModel.ConfiguredHotkey;
        TxtConfigPath.Text = _viewModel.ConfigFilePath;

        if (_viewModel.Apps.Count > 0)
        {
            LstApps.SelectedIndex = 0;
        }
        UpdateReorderButtonStates();
    }

    private void UpdateReorderButtonStates()
    {
        if (BtnMoveUp == null || BtnMoveDown == null || BtnRemove == null) return;
        int index = LstApps.SelectedIndex;
        BtnMoveUp.IsEnabled = index > 0;
        BtnMoveDown.IsEnabled = index >= 0 && index < _viewModel.Apps.Count - 1;
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
            TxtBootstrapIcon.Text = app.BootstrapIcon;
            TxtCustomGlyphSvg.Text = app.CustomGlyphSvg;
            TxtSvgOverride.Text = app.SvgOverride;

            UpdateTypeBadge(app);
            UpdateColorPreviews();
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
            TxtBootstrapIcon.Text = "";
            TxtCustomGlyphSvg.Text = "";
            TxtSvgOverride.Text = "";
            LblTypeBadge.Text = "None Selected";
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
        else if (sender == TxtSecondaryColor) { app.SecondaryColor = TxtSecondaryColor.Text; UpdateColorPreviews(); }
        else if (sender == TxtGlyphColor) { app.CustomGlyphColor = TxtGlyphColor.Text; UpdateColorPreviews(); }
        else if (sender == TxtLabel) app.Label = TxtLabel.Text;
        else if (sender == TxtFaviconPath) app.FaviconPath = TxtFaviconPath.Text;
        else if (sender == TxtBootstrapIcon) app.BootstrapIcon = TxtBootstrapIcon.Text;
        else if (sender == TxtCustomGlyphSvg) app.CustomGlyphSvg = TxtCustomGlyphSvg.Text;
        else if (sender == TxtSvgOverride) app.SvgOverride = TxtSvgOverride.Text;

        // Debounce icon regeneration preview
        _debounceTimer?.Dispose();
        _debounceTimer = new System.Threading.Timer(DebouncedRegeneratePreview, app, 300, System.Threading.Timeout.Infinite);
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
        using var dialog = new ColorDialog();
        try
        {
            if (!string.IsNullOrEmpty(TxtColor.Text))
            {
                dialog.Color = System.Drawing.ColorTranslator.FromHtml(TxtColor.Text);
            }
        }
        catch { }

        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            TxtColor.Text = $"#{dialog.Color.R:X2}{dialog.Color.G:X2}{dialog.Color.B:X2}";
        }
    }

    private void BtnPickSecondaryColor_Click(object sender, RoutedEventArgs e)
    {
        using var dialog = new ColorDialog();
        try
        {
            if (!string.IsNullOrEmpty(TxtSecondaryColor.Text))
            {
                dialog.Color = System.Drawing.ColorTranslator.FromHtml(TxtSecondaryColor.Text);
            }
        }
        catch { }

        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            TxtSecondaryColor.Text = $"#{dialog.Color.R:X2}{dialog.Color.G:X2}{dialog.Color.B:X2}";
        }
    }

    private void BtnPickGlyphColor_Click(object sender, RoutedEventArgs e)
    {
        using var dialog = new ColorDialog();
        try
        {
            if (!string.IsNullOrEmpty(TxtGlyphColor.Text))
            {
                dialog.Color = System.Drawing.ColorTranslator.FromHtml(TxtGlyphColor.Text);
            }
        }
        catch { }

        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            TxtGlyphColor.Text = $"#{dialog.Color.R:X2}{dialog.Color.G:X2}{dialog.Color.B:X2}";
        }
    }

    private void BtnBrowseConfigPath_Click(object sender, RoutedEventArgs e)
    {
        using var dialog = new System.Windows.Forms.OpenFileDialog
        {
            Title = "Select Catalyst Configuration File",
            Filter = "YAML Files (*.yaml;*.yml)|*.yaml;*.yml|All Files (*.*)|*.*",
            InitialDirectory = Path.GetDirectoryName(ConfigFilePath) ?? AppDomain.CurrentDomain.BaseDirectory
        };

        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            TxtConfigPath.Text = dialog.FileName;
            LoadConfigFromPath(dialog.FileName);
        }
    }

    private void BtnLoadConfigPath_Click(object sender, RoutedEventArgs e)
    {
        string path = TxtConfigPath.Text?.Trim() ?? "";
        if (!string.IsNullOrEmpty(path))
        {
            LoadConfigFromPath(path);
        }
    }

    private void BtnResetConfigPath_Click(object sender, RoutedEventArgs e)
    {
        string defaultPath = Catalyst.Services.ConfigService.GetDefaultConfigPath();
        TxtConfigPath.Text = defaultPath;
        LoadConfigFromPath(defaultPath);
    }

    private void LoadConfigFromPath(string path)
    {
        try
        {
            _viewModel.ConfigFilePath = path;
            _viewModel.Initialize(null, null, path);
            TxtConfigPath.Text = path;
            TxtGlobalHotkey.Text = _viewModel.ConfiguredHotkey;

            if (_viewModel.Apps.Count > 0)
            {
                LstApps.SelectedIndex = 0;
            }

            LblGenesisStatus.Text = $"Loaded from {Path.GetFileName(path)}";
            LblGenesisStatus.Foreground = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#60A5FA"));
        }
        catch (Exception ex)
        {
            LblGenesisStatus.Text = "Error loading configuration";
            LblGenesisStatus.Foreground = System.Windows.Media.Brushes.Red;
            System.Windows.MessageBox.Show($"Error loading configuration: {ex.Message}", "Catalyst", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }

    private void RegeneratePreview(AppInfo app)
    {
        try
        {
            var preview = _viewModel.RenderPreview(app);
            if (preview != null)
            {
                app.PreviewSource = preview;
            }
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

        using var dialog = new System.Windows.Forms.OpenFileDialog
        {
            Title = "Select Custom Glyph SVG File",
            InitialDirectory = Path.Combine(rootDir, "icons"),
            Filter = "SVG Files (*.svg)|*.svg|All Files (*.*)|*.*"
        };

        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            string path = dialog.FileName;
            if (path.StartsWith(rootDir, StringComparison.OrdinalIgnoreCase))
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

        using var dialog = new System.Windows.Forms.OpenFileDialog
        {
            Title = "Select Complete SVG Override File",
            InitialDirectory = Path.Combine(rootDir, "icons"),
            Filter = "SVG Files (*.svg)|*.svg|All Files (*.*)|*.*"
        };

        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            string path = dialog.FileName;
            if (path.StartsWith(rootDir, StringComparison.OrdinalIgnoreCase))
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

        using var dialog = new System.Windows.Forms.OpenFileDialog
        {
            Title = "Select Project Favicon Target",
            InitialDirectory = rootDir,
            Filter = "Icon Files (*.ico;*.png)|*.ico;*.png|All Files (*.*)|*.*"
        };

        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            string path = dialog.FileName;
            if (path.StartsWith(rootDir, StringComparison.OrdinalIgnoreCase))
            {
                path = Path.GetRelativePath(rootDir, path);
            }
            TxtFaviconPath.Text = path;
        }
    }

    private void BtnRegenerateCurrent_Click(object sender, RoutedEventArgs e)
    {
        if (LstApps.SelectedItem is not AppInfo app) return;

        try
        {
            _viewModel.GenerateIcon(app, (msg) => Debug.WriteLine(msg));

            string iconPath = Path.Combine(_viewModel.RootDir, "icons", app.Name, $"{app.Name}.png");
            if (File.Exists(iconPath))
            {
                app.IconPath = iconPath;
            }

            app.RefreshIcons();
            RegeneratePreview(app);
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

    private void BtnApplyHotkey_Click(object sender, RoutedEventArgs e)
    {
        string newHotkey = TxtGlobalHotkey.Text?.Trim() ?? "Alt+Space";
        ConfiguredHotkey = newHotkey;
        LblGenesisStatus.Text = $"Hotkey set to {newHotkey}";
        LblGenesisStatus.Foreground = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#60A5FA"));
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

    private void BtnMoveUp_Click(object sender, RoutedEventArgs e)
    {
        MoveSelectedAppUp();
    }

    private void BtnMoveDown_Click(object sender, RoutedEventArgs e)
    {
        MoveSelectedAppDown();
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
        string targetConfigPath = TxtConfigPath.Text?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(targetConfigPath))
        {
            targetConfigPath = _viewModel.ConfigFilePath;
        }
        else
        {
            targetConfigPath = Path.GetFullPath(targetConfigPath);
        }

        ConfigFilePath = targetConfigPath;

        try
        {
            ConfiguredHotkey = TxtGlobalHotkey.Text?.Trim() ?? "Alt+Space";
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
        BtnUpdateFavicons.IsEnabled = false;
        LblGenesisStatus.Text = "Generating icons...";
        LblGenesisStatus.Foreground = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#58A6FF"));

        try
        {
            string rootDir = RootDir;

            await Task.Run(() =>
            {
                _viewModel.GenerateAllIcons((msg) => Debug.WriteLine(msg));
            });

            foreach (var app in _viewModel.Apps)
            {
                string iconPath = Path.Combine(rootDir, "icons", app.Name, $"{app.Name}.png");
                if (File.Exists(iconPath))
                {
                    app.IconPath = iconPath;
                }
                app.RefreshIcons();
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
            BtnUpdateFavicons.IsEnabled = true;
        }
    }

    private async void BtnUpdateFavicons_Click(object sender, RoutedEventArgs e)
    {
        BtnUpdateFavicons.IsEnabled = false;
        BtnGenerate.IsEnabled = false;
        LblGenesisStatus.Text = "Updating Favicons...";
        LblGenesisStatus.Foreground = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#58A6FF"));

        try
        {
            string rootDir = RootDir;

            int updatedCount = 0;
            await Task.Run(() =>
            {
                foreach (var app in _viewModel.Apps)
                {
                    if (string.IsNullOrEmpty(app.FaviconPath)) continue;

                    string fullFaviconPath = Path.IsPathRooted(app.FaviconPath)
                        ? app.FaviconPath
                        : Path.GetFullPath(Path.Combine(rootDir, app.FaviconPath));

                    string generatedIcoPath = Path.Combine(rootDir, "icons", app.Name, "favicon.ico");
                    string generatedPngPath = Path.Combine(rootDir, "icons", app.Name, "favicon-32x32.png");

                    if (File.Exists(generatedIcoPath) && fullFaviconPath.EndsWith(".ico", StringComparison.OrdinalIgnoreCase))
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(fullFaviconPath)!);
                        File.Copy(generatedIcoPath, fullFaviconPath, true);
                        updatedCount++;

                        if (!string.IsNullOrEmpty(app.ProjectPath) && app.ProjectPath.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
                        {
                            UpdateCsprojApplicationIcon(app.ProjectPath, fullFaviconPath, rootDir);
                        }
                    }
                    else if (File.Exists(generatedPngPath) && fullFaviconPath.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(fullFaviconPath)!);
                        File.Copy(generatedPngPath, fullFaviconPath, true);
                        updatedCount++;
                    }
                }
            });

            LblGenesisStatus.Text = $"Updated {updatedCount} projects";
            LblGenesisStatus.Foreground = System.Windows.Media.Brushes.Green;
            System.Windows.MessageBox.Show($"Successfully updated {updatedCount} project favicons.");
        }
        catch (Exception ex)
        {
            LblGenesisStatus.Text = "Error";
            LblGenesisStatus.Foreground = System.Windows.Media.Brushes.Red;
            System.Windows.MessageBox.Show($"Error updating favicons: {ex.Message}");
        }
        finally
        {
            BtnUpdateFavicons.IsEnabled = true;
            BtnGenerate.IsEnabled = true;
        }
    }

    private void UpdateCsprojApplicationIcon(string projectPath, string iconPath, string rootDir)
    {
        try
        {
            string fullProjectPath = Path.IsPathRooted(projectPath) ? projectPath : Path.GetFullPath(Path.Combine(rootDir, projectPath));
            if (!File.Exists(fullProjectPath)) return;

            string projectDir = Path.GetDirectoryName(fullProjectPath)!;
            string relativeIconPath = Path.GetRelativePath(projectDir, iconPath);

            string content = File.ReadAllText(fullProjectPath);
            bool modified = false;

            if (content.Contains("<ApplicationIcon>"))
            {
                var regex = new System.Text.RegularExpressions.Regex(@"<ApplicationIcon>.*?</ApplicationIcon>");
                string newContent = regex.Replace(content, $"<ApplicationIcon>{relativeIconPath}</ApplicationIcon>");
                if (newContent != content)
                {
                    content = newContent;
                    modified = true;
                }
            }
            else
            {
                int index = content.IndexOf("</PropertyGroup>");
                if (index > 0)
                {
                    content = content.Insert(index, $"    <ApplicationIcon>{relativeIconPath}</ApplicationIcon>\n    ");
                    modified = true;
                }
            }

            if (modified)
            {
                File.WriteAllText(fullProjectPath, content);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to update csproj {projectPath}: {ex.Message}");
        }
    }
}
