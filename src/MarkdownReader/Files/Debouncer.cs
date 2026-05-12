using System;
using System.Threading;

namespace MarkdownReader.Files;

/// <summary>
/// Collapses rapid Trigger() calls into a single delayed callback invocation.
/// Each Trigger reschedules the pending fire to (now + delay), so a burst of
/// triggers within the delay window produces exactly one callback.
/// </summary>
/// <remarks>
/// The callback is invoked on a thread-pool thread. It is NOT serialized with
/// Trigger or Dispose; callers that need UI-thread execution should marshal
/// inside the callback (e.g., Dispatcher.InvokeAsync). Exceptions thrown by
/// the callback are caught and swallowed (the timer keeps working); attach a
/// debugger or wrap your callback in try/log if you need to observe them.
/// </remarks>
public sealed class Debouncer : IDisposable
{
    private readonly Timer _timer;
    private readonly TimeSpan _delay;
    private readonly Action _callback;
    private readonly object _lock = new();
    private bool _disposed;

    public Debouncer(TimeSpan delay, Action callback)
    {
        ArgumentNullException.ThrowIfNull(callback);
        _delay = delay;
        _callback = callback;
        _timer = new Timer(OnTimer, null, Timeout.Infinite, Timeout.Infinite);
    }

    public void Trigger()
    {
        lock (_lock)
        {
            if (_disposed) return;
            _timer.Change(_delay, Timeout.InfiniteTimeSpan);
        }
    }

    private void OnTimer(object? state)
    {
        // Snapshot disposed flag under lock, but invoke callback OUTSIDE the lock.
        // This avoids deadlocks where the callback marshals to a thread that may
        // itself be calling Trigger/Dispose.
        bool disposed;
        lock (_lock) { disposed = _disposed; }
        if (disposed) return;

        try { _callback(); }
        catch { /* swallow: don't let user callback errors crash the timer thread */ }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            if (_disposed) return;
            _disposed = true;
        }
        _timer.Dispose();
    }
}
