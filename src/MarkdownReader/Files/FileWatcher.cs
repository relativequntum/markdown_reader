using System;
using System.IO;

namespace MarkdownReader.Files;

public sealed class FileWatcher : IDisposable
{
    private readonly FileSystemWatcher _w;
    private readonly Debouncer _debounce;
    private string _filePath;
    public event Action? Changed;
    public event Action<string>? Renamed;
    public event Action? Deleted;

    public FileWatcher(string filePath, TimeSpan debounce)
    {
        _filePath = filePath;
        _debounce = new Debouncer(debounce, () => Changed?.Invoke());
        _w = new FileSystemWatcher(Path.GetDirectoryName(filePath)!, Path.GetFileName(filePath))
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size,
            EnableRaisingEvents = true
        };
        _w.Changed += (_, e) =>
        {
            if (string.Equals(e.FullPath, _filePath, StringComparison.OrdinalIgnoreCase))
                _debounce.Trigger();
        };
        _w.Renamed += (_, e) =>
        {
            if (string.Equals(e.OldFullPath, _filePath, StringComparison.OrdinalIgnoreCase))
            {
                _filePath = e.FullPath;
                _w.Filter = Path.GetFileName(_filePath);
                Renamed?.Invoke(_filePath);
            }
        };
        _w.Deleted += (_, e) =>
        {
            if (string.Equals(e.FullPath, _filePath, StringComparison.OrdinalIgnoreCase))
                Deleted?.Invoke();
        };
    }

    public void Dispose()
    {
        _w.Dispose();
        _debounce.Dispose();
    }
}
