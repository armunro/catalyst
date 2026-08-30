using System;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using Wpf.Ui.Controls;

namespace Catalyst.Windows;

public partial class LogViewerWindow : FluentWindow
{
    private readonly AppInfo _app;
    private static readonly Regex UrlRegex = new Regex(@"(https?://[^\s]+)", RegexOptions.Compiled);
    private int _lineCount = 0;

    public LogViewerWindow(AppInfo app)
    {
        InitializeComponent();
        _app = app;
        Title = $"Catalyst - {app.Name} Logs";
        TxtAppName.Text = $"{app.Name} Logs";
        UpdateStatusPill();

        _app.OnLogReceived += App_OnLogReceived;
        _app.PropertyChanged += App_PropertyChanged;
        
        LoadExistingLogs();
        
        Closing += (s, e) =>
        {
            _app.OnLogReceived -= App_OnLogReceived;
            _app.PropertyChanged -= App_PropertyChanged;
        };
    }

    private void App_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AppInfo.IsRunning) || e.PropertyName == nameof(AppInfo.IsRunningString))
        {
            Dispatcher.Invoke(UpdateStatusPill);
        }
    }

    private void UpdateStatusPill()
    {
        TxtAppStatus.Text = _app.IsRunningString;
        TxtAppStatus.Foreground = _app.IsRunning
            ? new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#4ADE80"))
            : new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#94A3B8"));

        StatusDot.Fill = _app.IsRunning
            ? new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#4ADE80"))
            : new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#94A3B8"));
    }

    private void LoadExistingLogs()
    {
        lock (_app.Logs)
        {
            foreach (var log in _app.Logs)
            {
                AppendLogLine(log);
            }
        }
    }

    private void App_OnLogReceived(string message)
    {
        Dispatcher.BeginInvoke(new Action(() =>
        {
            if (message == null)
            {
                RtbLogs.Document.Blocks.Clear();
                _lineCount = 0;
                UpdateLineCount();
            }
            else
            {
                AppendLogLine(message);
            }
        }));
    }

    private void AppendLogLine(string logLine)
    {
        var paragraph = new Paragraph { Margin = new Thickness(0) };
        
        var parts = UrlRegex.Split(logLine);
        foreach (var part in parts)
        {
            if (UrlRegex.IsMatch(part))
            {
                var hyperlink = new Hyperlink(new Run(part))
                {
                    NavigateUri = new Uri(part),
                    Foreground = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#60A5FA"))
                };
                hyperlink.RequestNavigate += (s, e) =>
                {
                    Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
                    e.Handled = true;
                };
                paragraph.Inlines.Add(hyperlink);
            }
            else
            {
                paragraph.Inlines.Add(new Run(part));
            }
        }

        RtbLogs.Document.Blocks.Add(paragraph);
        _lineCount++;
        UpdateLineCount();
        RtbLogs.ScrollToEnd();
    }

    private void UpdateLineCount()
    {
        TxtLineCount.Text = $"{_lineCount} lines";
    }

    private void BtnClear_Click(object sender, RoutedEventArgs e)
    {
        _app.ClearLogs();
    }

    private void BtnCopy_Click(object sender, RoutedEventArgs e)
    {
        lock (_app.Logs)
        {
            var sb = new StringBuilder();
            foreach (var line in _app.Logs)
            {
                sb.AppendLine(line);
            }
            if (sb.Length > 0)
            {
                System.Windows.Clipboard.SetText(sb.ToString());
            }
        }
    }
}
