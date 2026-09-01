using System;
using System.IO;
using System.Windows;
using Catalyst.Adapters.UI.ViewModels;
using Catalyst.Core.Ports.Inbound;
using Microsoft.Extensions.DependencyInjection;
using Wpf.Ui.Controls;

namespace Catalyst.Windows;

public partial class SettingsWindow : FluentWindow
{
    private readonly SettingsViewModel _viewModel;

    public SettingsViewModel ViewModel => _viewModel;

    public SettingsWindow() : this(CreateDefaultViewModel())
    {
    }

    private static SettingsViewModel CreateDefaultViewModel()
    {
        if (App.Services != null)
        {
            return App.Services.GetRequiredService<SettingsViewModel>();
        }

        var settings = new Adapters.Persistence.JsonSettingsStorage();
        var resolver = new Adapters.Persistence.PathResolver(settings);
        var repo = new Adapters.Persistence.YamlConfigRepository();
        var configService = new Core.Services.AppConfigurationService(repo, settings, resolver);
        var hotkeyHook = new Adapters.Platform.WindowsHotkeyHook();
        var hotkeyService = new Core.Services.HotkeyService(hotkeyHook);

        return new SettingsViewModel(configService, hotkeyService);
    }

    public SettingsWindow(SettingsViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;
    }

    private void BtnBrowseConfigPath_Click(object sender, RoutedEventArgs e)
    {
        using var dialog = new System.Windows.Forms.OpenFileDialog
        {
            Title = "Select Catalyst Configuration File",
            Filter = "YAML Files (*.yaml;*.yml)|*.yaml;*.yml|All Files (*.*)|*.*",
            InitialDirectory = Path.GetDirectoryName(_viewModel.ConfigFilePath) ?? AppDomain.CurrentDomain.BaseDirectory
        };

        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            _viewModel.ConfigFilePath = dialog.FileName;
            _viewModel.LoadConfig(dialog.FileName);
        }
    }

    private void BtnLoadConfigPath_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.LoadConfig();
    }

    private void BtnResetConfigPath_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.ResetToDefault();
    }

    private void BtnBrowseIconsDir_Click(object sender, RoutedEventArgs e)
    {
        using var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = "Select Output Icon Directory",
            UseDescriptionForTitle = true,
            SelectedPath = Directory.Exists(_viewModel.IconsDirectory) ? _viewModel.IconsDirectory : AppDomain.CurrentDomain.BaseDirectory
        };

        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            _viewModel.IconsDirectory = dialog.SelectedPath;
        }
    }

    private void BtnResetIconsDir_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.ResetIconsDirectoryToDefault();
    }

    private void BtnApplyHotkey_Click(object sender, RoutedEventArgs e)
    {
        string hotkey = _viewModel.ConfiguredHotkey?.Trim() ?? "";
        if (_viewModel.ValidateHotkey(hotkey))
        {
            _viewModel.Save();
        }
        else
        {
            System.Windows.MessageBox.Show($"'{hotkey}' is not a valid hotkey combination. Try e.g. Alt+Space or Ctrl+Shift+C.", "Invalid Hotkey");
        }
    }

    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.Save();
        DialogResult = true;
        Close();
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
