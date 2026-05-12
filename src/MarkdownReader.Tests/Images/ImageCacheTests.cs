using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MarkdownReader.Files;
using MarkdownReader.Images;
using Xunit;

namespace MarkdownReader.Tests.Images;

public class ImageCacheTests
{
    [Fact]
    public void Miss_Then_Put_Then_Hit()
    {
        var clock = new FakeClock();
        var fs = new InMemFs(clock);
        var cache = new ImageCache(@"C:\cache", fs, clock);

        Assert.False(cache.TryGet("http://a", out _, out _));
        cache.Put("http://a", new byte[]{1,2,3}, "image/png");
        Assert.True(cache.TryGet("http://a", out var bytes, out var meta));
        Assert.Equal(new byte[]{1,2,3}, bytes);
        Assert.Equal("image/png", meta.ContentType);
        Assert.Equal("http://a", meta.Url);
    }

    [Fact]
    public void EnforceLimits_EvictsOldestByAccessTime()
    {
        var clock = new FakeClock();
        var fs = new InMemFs(clock);
        var cache = new ImageCache(@"C:\cache", fs, clock);

        cache.Put("u1", new byte[100], "image/png");
        clock.Advance(TimeSpan.FromMinutes(1));
        cache.Put("u2", new byte[100], "image/png");
        clock.Advance(TimeSpan.FromMinutes(1));
        cache.Put("u3", new byte[100], "image/png");

        cache.EnforceLimits(maxBytes: 250, maxFiles: 10);
        Assert.False(cache.TryGet("u1", out _, out _));   // oldest evicted
        Assert.True(cache.TryGet("u2", out _, out _));
        Assert.True(cache.TryGet("u3", out _, out _));
    }

    [Fact]
    public void TryGet_RefreshesAccessTime()
    {
        var clock = new FakeClock();
        var fs = new InMemFs(clock);
        var cache = new ImageCache(@"C:\cache", fs, clock);

        cache.Put("u1", new byte[100], "image/png");
        clock.Advance(TimeSpan.FromMinutes(1));
        cache.Put("u2", new byte[100], "image/png");
        clock.Advance(TimeSpan.FromMinutes(1));

        cache.TryGet("u1", out _, out _);   // touches u1

        clock.Advance(TimeSpan.FromMinutes(1));
        cache.Put("u3", new byte[100], "image/png");

        cache.EnforceLimits(maxBytes: 250, maxFiles: 10);
        // Now u2 is the oldest; u1 was refreshed
        Assert.False(cache.TryGet("u2", out _, out _));
        Assert.True(cache.TryGet("u1", out _, out _));
        Assert.True(cache.TryGet("u3", out _, out _));
    }

    [Fact]
    public void EnforceLimits_StopsWhenWithinBudget()
    {
        var clock = new FakeClock();
        var fs = new InMemFs(clock);
        var cache = new ImageCache(@"C:\cache", fs, clock);
        cache.Put("u1", new byte[100], "image/png");
        cache.Put("u2", new byte[100], "image/png");
        cache.EnforceLimits(maxBytes: 1_000_000, maxFiles: 1_000);   // already under
        Assert.True(cache.TryGet("u1", out _, out _));
        Assert.True(cache.TryGet("u2", out _, out _));
    }

    [Fact]
    public void EnforceLimits_ByFileCount()
    {
        var clock = new FakeClock();
        var fs = new InMemFs(clock);
        var cache = new ImageCache(@"C:\cache", fs, clock);
        cache.Put("u1", new byte[10], "image/png"); clock.Advance(TimeSpan.FromMinutes(1));
        cache.Put("u2", new byte[10], "image/png"); clock.Advance(TimeSpan.FromMinutes(1));
        cache.Put("u3", new byte[10], "image/png");

        cache.EnforceLimits(maxBytes: 1_000_000, maxFiles: 2);
        Assert.False(cache.TryGet("u1", out _, out _));
        Assert.True(cache.TryGet("u2", out _, out _));
        Assert.True(cache.TryGet("u3", out _, out _));
    }
}

// ---- test helpers (live in same file for simplicity) ----

internal sealed class FakeClock : TimeProvider
{
    private DateTimeOffset _now = new DateTimeOffset(2026, 5, 12, 0, 0, 0, TimeSpan.Zero);
    public override DateTimeOffset GetUtcNow() => _now;
    public void Advance(TimeSpan d) => _now = _now.Add(d);
}

internal sealed class InMemFs : IFileSystem
{
    private readonly FakeClock _clock;
    private readonly Dictionary<string, byte[]> _files = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, DateTime> _atime = new(StringComparer.OrdinalIgnoreCase);

    public InMemFs(FakeClock clock) => _clock = clock;

    public bool FileExists(string p) => _files.ContainsKey(p);
    public byte[] ReadAllBytes(string p) => _files[p];
    public void WriteAllBytes(string p, byte[] d) { _files[p] = d; _atime[p] = _clock.GetUtcNow().UtcDateTime; }
    public void Delete(string p) { _files.Remove(p); _atime.Remove(p); }
    public DateTime GetLastAccessTime(string p) => _atime[p];
    public void SetLastAccessTime(string p, DateTime t) => _atime[p] = t;
    public IEnumerable<string> EnumerateFiles(string d, string pat)
    {
        var prefix = d.TrimEnd('\\', '/') + "\\";
        return _files.Keys.Where(k =>
            k.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) &&
            (pat == "*.bin" ? k.EndsWith(".bin") : true));
    }
    public long GetSize(string p) => _files[p].LongLength;
    public void EnsureDir(string d) { /* no-op for in-mem */ }
}
