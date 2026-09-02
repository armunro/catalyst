using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace Catalyst.Adapters.Platform;

public class SingleInstanceManager : IDisposable
{
    private const string DefaultMutexName = "Catalyst_SingleInstance_Mutex_9D3182B0-0435-4D04-87A5-E120A05D3D10";
    private const string DefaultEventName = "Catalyst_SingleInstance_ShowEvent_9D3182B0-0435-4D04-87A5-E120A05D3D10";

    [DllImport("user32.dll")]
    private static extern bool AllowSetForegroundWindow(int dwProcessId);
    private const int ASFW_ANY = -1;

    private readonly string _mutexName;
    private readonly string _eventName;
    private Mutex? _mutex;
    private EventWaitHandle? _eventWaitHandle;
    private RegisteredWaitHandle? _registeredWaitHandle;
    private bool _hasMutex;

    public SingleInstanceManager(string? mutexName = null, string? eventName = null)
    {
        _mutexName = mutexName ?? DefaultMutexName;
        _eventName = eventName ?? DefaultEventName;
    }

    public bool TryAcquireSingleInstance(Action onAnotherInstanceLaunched)
    {
        try
        {
            _mutex = new Mutex(true, _mutexName, out _hasMutex);
        }
        catch (AbandonedMutexException)
        {
            _hasMutex = true;
        }

        if (!_hasMutex)
        {
            return false;
        }

        try
        {
            _eventWaitHandle = new EventWaitHandle(false, EventResetMode.AutoReset, _eventName);
            _registeredWaitHandle = ThreadPool.RegisterWaitForSingleObject(
                _eventWaitHandle,
                (state, timedOut) =>
                {
                    if (!timedOut)
                    {
                        onAnotherInstanceLaunched?.Invoke();
                    }
                },
                null,
                -1,
                false);
        }
        catch
        {
            // If event registration fails, we still hold the mutex
        }

        return true;
    }

    public void NotifyExistingInstance()
    {
        try
        {
            AllowSetForegroundWindow(ASFW_ANY);
            if (EventWaitHandle.TryOpenExisting(_eventName, out var showEvent))
            {
                showEvent.Set();
                showEvent.Dispose();
            }
        }
        catch
        {
            // Ignore error if signaling fails
        }
    }

    public void Dispose()
    {
        _registeredWaitHandle?.Unregister(null);
        _registeredWaitHandle = null;

        _eventWaitHandle?.Dispose();
        _eventWaitHandle = null;

        if (_hasMutex && _mutex != null)
        {
            try
            {
                _mutex.ReleaseMutex();
            }
            catch
            {
            }
            _mutex.Dispose();
            _mutex = null;
            _hasMutex = false;
        }
    }
}
