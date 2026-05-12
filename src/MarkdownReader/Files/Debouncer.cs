using System;
using System.Threading;

namespace MarkdownReader.Files;

public sealed class Debouncer : IDisposable
{
    private readonly Timer _timer;
    private readonly TimeSpan _delay;
    private readonly Action _callback;
    private readonly object _lock = new();
    private bool _disposed;

    public Debouncer(TimeSpan delay, Action callback)
    {
        _delay = delay;
        _callback = callback;
        _timer = new Timer(_ => { lock (_lock) { if (!_disposed) _callback(); } },
                           null, Timeout.Infinite, Timeout.Infinite);
    }

    public void Trigger()
    {
        lock (_lock) { if (!_disposed) _timer.Change(_delay, Timeout.InfiniteTimeSpan); }
    }

    public void Dispose()
    {
        lock (_lock) { _disposed = true; _timer.Dispose(); }
    }
}
