using System;
using System.Threading;
using System.Threading.Tasks;
using MarkdownReader.Files;
using Xunit;

namespace MarkdownReader.Tests.Files;

public class DebouncerTests
{
    [Fact]
    public async Task SingleHit_FiresOnce()
    {
        int n = 0;
        var d = new Debouncer(TimeSpan.FromMilliseconds(100), () => Interlocked.Increment(ref n));
        d.Trigger();
        await Task.Delay(250);
        Assert.Equal(1, n);
    }

    [Fact]
    public async Task RapidHits_FireOnce()
    {
        int n = 0;
        var d = new Debouncer(TimeSpan.FromMilliseconds(100), () => Interlocked.Increment(ref n));
        for (int i = 0; i < 10; i++) { d.Trigger(); await Task.Delay(20); }
        await Task.Delay(250);
        Assert.Equal(1, n);
    }

    [Fact]
    public async Task SpacedHits_FireTwice()
    {
        int n = 0;
        var d = new Debouncer(TimeSpan.FromMilliseconds(80), () => Interlocked.Increment(ref n));
        d.Trigger();
        await Task.Delay(200);
        d.Trigger();
        await Task.Delay(200);
        Assert.Equal(2, n);
    }
}
