using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Media;
using Catalyst.Services;
using Wpf.Ui.Controls;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Catalyst.Windows;

public partial class AppManagementWindow : FluentWindow
{
    private readonly ObservableCollection<AppInfo> _apps;
    private bool _isUpdatingConfig = false;
    private System.Threading.Timer? _debounceTimer;

    public string ConfiguredHotkey { get; set; } = "Alt+Space";
    public string ConfigFilePath { get; set; } = string.Empty;
    public string RootDir => ConfigService.GetRootDir(ConfigFilePath);

    public AppManagementWindow(ObservableCollection<AppInfo> apps, string initialHotkey = "Alt+Space", string? initialConfigPath = null)
    {
        InitializeComponent();
        _apps = apps;
        LstApps.ItemsSource = _apps;
        ConfiguredHotkey = string.IsNullOrWhiteSpace(initialHotkey) ? "Alt+Space" : initialHotkey;
        TxtGlobalHotkey.Text = ConfiguredHotkey;

        ConfigFilePath = !string.IsNullOrWhiteSpace(initialConfigPath)
            ? initialConfigPath
            : ConfigService.GetActiveConfigPath();
        TxtConfigPath.Text = ConfigFilePath;

        if (_apps.Count > 0)
        {
            LstApps.SelectedIndex = 0;
        }
    }

    private void LstApps_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (LstApps.SelectedItem is AppInfo app)
        {
            _isUpdatingConfig = true;
            TxtAppName.Text = app.Name;
            TxtProjectPath.Text = app.LaunchTarget;
            TxtArguments.Text = app.Arguments;
            TxtWorkingDirectory.Text = app.WorkingDirectory;
            ChkRunAsAdmin.IsChecked = app.RunAsAdmin;

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
            TxtAppName.Text = "";
            TxtProjectPath.Text = "";
            TxtArguments.Text = "";
            TxtWorkingDirectory.Text = "";
            ChkRunAsAdmin.IsChecked = false;
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

        // Debounce icon regeneration preview
        _debounceTimer?.Dispose();
        _debounceTimer = new System.Threading.Timer(DebouncedRegeneratePreview, app, 300, System.Threading.Timeout.Infinite);
    }

    private void Config_CheckChanged(object sender, RoutedEventArgs e)
    {
        if (_isUpdatingConfig || LstApps.SelectedItem is not AppInfo app) return;
        app.RunAsAdmin = ChkRunAsAdmin.IsChecked == true;
    }

    private void DebouncedRegeneratePreview(object? state)
    {
        if (state is not AppInfo app) return;

        Dispatcher.Invoke(() =>
        {
            RegeneratePreview(app);
        });
    }

    private void BtnBrowseConfigPath_Click(object sender, RoutedEventArgs e)
    {
        using var dialog = new System.Windows.Forms.OpenFileDialog
        {
            Title = "Select apps.yaml Configuration File",
            Filter = "YAML Files (*.yaml;*.yml)|*.yaml;*.yml|All Files (*.*)|*.*",
            FileName = Path.GetFileName(ConfigFilePath),
            InitialDirectory = Directory.Exists(RootDir) ? RootDir : AppDomain.CurrentDomain.BaseDirectory
        };

        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            TxtConfigPath.Text = dialog.FileName;
            LoadFromConfigPath(dialog.FileName);
        }
    }

    private void BtnLoadConfigPath_Click(object sender, RoutedEventArgs e)
    {
        string path = TxtConfigPath.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(path))
        {
            System.Windows.MessageBox.Show("Please enter a valid configuration file path.", "Catalyst", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            return;
        }

        if (!File.Exists(path))
        {
            System.Windows.MessageBox.Show($"Configuration file not found: {path}", "Catalyst", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            return;
        }

        LoadFromConfigPath(path);
    }

    private void BtnResetConfigPath_Click(object sender, RoutedEventArgs e)
    {
        string defaultPath = ConfigService.GetDefaultConfigPath();
        TxtConfigPath.Text = defaultPath;
        if (File.Exists(defaultPath))
        {
            LoadFromConfigPath(defaultPath);
        }
    }

    public void LoadFromConfigPath(string path)
    {
        try
        {
            var config = ConfigService.LoadConfigFile(path);
            if (config != null)
            {
                ConfigFilePath = path;
                TxtConfigPath.Text = path;
                if (!string.IsNullOrWhiteSpace(config.Hotkey))
                {
                    ConfiguredHotkey = config.Hotkey;
                    TxtGlobalHotkey.Text = config.Hotkey;
                }

                string rootDir = ConfigService.GetRootDir(path);
                _apps.Clear();
                foreach (var entry in config.Apps)
                {
                    string fullProjectPath = string.Empty;
                    string fullExecPath = string.Empty;

                    if (entry.Launch != null)
                    {
                        if (!string.IsNullOrEmpty(entry.Launch.ProjectPath))
                        {
                            fullProjectPath = Path.IsPathRooted(entry.Launch.ProjectPath)
                                ? entry.Launch.ProjectPath
                                : Path.GetFullPath(Path.Combine(rootDir, entry.Launch.ProjectPath));
                        }

                        if (!string.IsNullOrEmpty(entry.Launch.ExecutablePath))
                        {
                            fullExecPath = Path.IsPathRooted(entry.Launch.ExecutablePath)
                                ? entry.Launch.ExecutablePath
                                : Path.GetFullPath(Path.Combine(rootDir, entry.Launch.ExecutablePath));
                        }
                    }

                    var appInfo = new AppInfo
                    {
                        Name = entry.Name,
                        ProjectPath = fullProjectPath,
                        ExecutablePath = fullExecPath,
                        Arguments = entry.Launch?.Arguments ?? string.Empty,
                        WorkingDirectory = entry.Launch?.WorkingDirectory ?? string.Empty,
                        RunAsAdmin = entry.Launch?.RunAsAdmin ?? false
                    };

                    if (entry.Icon != null)
                    {
                        appInfo.Color = entry.Icon.Color;
                        appInfo.SecondaryColor = entry.Icon.SecondaryColor;
                        appInfo.BackgroundType = entry.Icon.BackgroundType;
                        appInfo.GradientDirection = entry.Icon.GradientDirection;
                        appInfo.Label = entry.Icon.Label;
                        appInfo.FaviconPath = entry.Icon.FaviconPath;
                        appInfo.BootstrapIcon = entry.Icon.BootstrapIcon;
                        appInfo.CustomGlyphSvg = entry.Icon.CustomGlyphSvg;
                        appInfo.CustomGlyphColor = entry.Icon.CustomGlyphColor;
                        appInfo.SvgOverride = entry.Icon.SvgOverride;

                        if (!string.IsNullOrEmpty(entry.Icon.IconPath))
                        {
                            appInfo.IconPath = Path.IsPathRooted(entry.Icon.IconPath)
                                ? entry.Icon.IconPath
                                : Path.GetFullPath(Path.Combine(rootDir, entry.Icon.IconPath));
                        }
                    }

                    if (string.IsNullOrEmpty(appInfo.IconPath))
                    {
                        string autoIconPath = Path.Combine(rootDir, "icons", entry.Name, $"{entry.Name}.png");
                        if (File.Exists(autoIconPath))
                        {
                            appInfo.IconPath = autoIconPath;
                        }
                    }

                    _apps.Add(appInfo);
                }

                if (_apps.Count > 0)
                {
                    LstApps.SelectedIndex = 0;
                }

                LblGenesisStatus.Text = $"Loaded from {Path.GetFileName(path)}";
                LblGenesisStatus.Foreground = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#60A5FA"));
            }
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
            string rootDir = RootDir;
            string iconsBaseDir = Path.Combine(rootDir, "icons");

            var generator = new IconGenerator();
            var preview = generator.RenderPreview(app, iconsBaseDir, rootDir, 512);
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
            string rootDir = RootDir;
            string iconsBaseDir = Path.Combine(rootDir, "icons");

            var generator = new IconGenerator();
            generator.GenerateIcon(app, iconsBaseDir, (msg) => Debug.WriteLine(msg), rootDir);

            string iconPath = Path.Combine(iconsBaseDir, app.Name, $"{app.Name}.png");
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
        var newApp = new AppInfo
        {
            Name = "NewApp",
            ExecutablePath = "",
            Color = "#1E88E4",
            SecondaryColor = "",
            BackgroundType = "Solid",
            GradientDirection = "Diagonal",
            Label = "NEW"
        };
        _apps.Add(newApp);
        LstApps.SelectedItem = newApp;
    }

    private void BtnRemove_Click(object sender, RoutedEventArgs e)
    {
        if (LstApps.SelectedItem is AppInfo app)
        {
            _apps.Remove(app);
        }
    }

    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        string targetConfigPath = TxtConfigPath.Text?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(targetConfigPath))
        {
            targetConfigPath = ConfigService.GetActiveConfigPath();
        }
        else
        {
            targetConfigPath = Path.GetFullPath(targetConfigPath);
        }

        ConfigFilePath = targetConfigPath;
        string rootDir = RootDir;

        try
        {
            ConfiguredHotkey = TxtGlobalHotkey.Text?.Trim() ?? "Alt+Space";

            var config = new AppConfigFile
            {
                Hotkey = ConfiguredHotkey
            };

            foreach (var app in _apps)
            {
                string projectPath = app.ProjectPath;
                if (!string.IsNullOrEmpty(projectPath) && projectPath.StartsWith(rootDir, StringComparison.OrdinalIgnoreCase))
                {
                    projectPath = Path.GetRelativePath(rootDir, projectPath);
                }

                string execPath = app.ExecutablePath;
                if (!string.IsNullOrEmpty(execPath) && execPath.StartsWith(rootDir, StringComparison.OrdinalIgnoreCase))
                {
                    execPath = Path.GetRelativePath(rootDir, execPath);
                }

                string iconPath = app.IconPath;
                if (!string.IsNullOrEmpty(iconPath) && iconPath.StartsWith(rootDir, StringComparison.OrdinalIgnoreCase))
                {
                    iconPath = Path.GetRelativePath(rootDir, iconPath);
                }

                string faviconPath = app.FaviconPath;
                if (!string.IsNullOrEmpty(faviconPath) && faviconPath.StartsWith(rootDir, StringComparison.OrdinalIgnoreCase))
                {
                    faviconPath = Path.GetRelativePath(rootDir, faviconPath);
                }

                string customGlyphSvg = app.CustomGlyphSvg;
                if (!string.IsNullOrEmpty(customGlyphSvg) && customGlyphSvg.StartsWith(rootDir, StringComparison.OrdinalIgnoreCase))
                {
                    customGlyphSvg = Path.GetRelativePath(rootDir, customGlyphSvg);
                }

                string svgOverride = app.SvgOverride;
                if (!string.IsNullOrEmpty(svgOverride) && svgOverride.StartsWith(rootDir, StringComparison.OrdinalIgnoreCase))
                {
                    svgOverride = Path.GetRelativePath(rootDir, svgOverride);
                }

                config.Apps.Add(new AppConfigEntry
                {
                    Name = app.Name,
                    Launch = new LaunchConfig
                    {
                        ProjectPath = projectPath,
                        ExecutablePath = execPath,
                        Arguments = app.Arguments,
                        WorkingDirectory = app.WorkingDirectory,
                        RunAsAdmin = app.RunAsAdmin
                    },
                    Icon = new IconConfig
                    {
                        Color = app.Color,
                        SecondaryColor = app.SecondaryColor,
                        BackgroundType = app.BackgroundType,
                        GradientDirection = app.GradientDirection,
                        Label = app.Label,
                        IconPath = iconPath,
                        FaviconPath = faviconPath,
                        BootstrapIcon = app.BootstrapIcon,
                        CustomGlyphSvg = customGlyphSvg,
                        CustomGlyphColor = app.CustomGlyphColor,
                        SvgOverride = svgOverride
                    }
                });
            }

            ConfigService.SaveConfigFile(config, targetConfigPath);
            ConfigService.SetUserConfigPath(targetConfigPath);

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
            
            var generatedIcons = new List<(string AppName, string IconPath)>();
            await Task.Run(() =>
            {
                var generator = new IconGenerator();
                generator.Generate(_apps.ToList(), rootDir, (msg) => Debug.WriteLine(msg));
            });

            foreach (var app in _apps)
            {
                string iconPath = Path.Combine(rootDir, "icons", app.Name, $"{app.Name}.png");
                if (File.Exists(iconPath))
                {
                    app.IconPath = iconPath;
                }
                app.RefreshIcons();
                generatedIcons.Add((app.Name, iconPath));
            }
            
            LblGenesisStatus.Text = "Completed";
            LblGenesisStatus.Foreground = System.Windows.Media.Brushes.Green;

            var resultWindow = new IconsResultWindow(Path.Combine(rootDir, "icons"), generatedIcons);
            resultWindow.Owner = this;
            resultWindow.ShowDialog();
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
                foreach (var app in _apps)
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
