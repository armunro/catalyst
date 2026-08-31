using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Catalyst.Adapters.UI.ViewModels;

public class LogViewerViewModel : INotifyPropertyChanged
{
    private AppInfo _app;

    public LogViewerViewModel(AppInfo app)
    {
        _app = app;
    }

    public AppInfo App
    {
        get => _app;
        set
        {
            if (_app != value)
            {
                _app = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(Title));
            }
        }
    }

    public string Title => $"Catalyst - {App.Name} Logs";

    public void ClearLogs()
    {
        App.ClearLogs();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
