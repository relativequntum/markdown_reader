using MarkdownReader.Images;
using Xunit;

namespace MarkdownReader.Tests.Images;

public class PathValidatorTests
{
    private static readonly string[] Whitelist =
    {
        @"C:\Docs",
        @"C:\Users\me",
        @"C:\Temp"
    };

    [Theory]
    [InlineData(@"C:\Docs\images\a.png", true)]
    [InlineData(@"C:\Docs\sub\images\a.png", true)]
    [InlineData(@"C:\Users\me\Pictures\b.jpg", true)]
    [InlineData(@"C:\Windows\system32\evil.dll", false)]
    [InlineData(@"C:\Docs\..\Windows\x.png", false)]
    [InlineData(@"\\server\share\x.png", false)]   // UNC 拒绝
    public void IsAllowed(string path, bool expected)
    {
        Assert.Equal(expected, PathValidator.IsAllowed(path, Whitelist));
    }

    [Fact]
    public void NullOrEmpty_NotAllowed()
    {
        Assert.False(PathValidator.IsAllowed("", Whitelist));
        Assert.False(PathValidator.IsAllowed(null!, Whitelist));
    }

    [Fact]
    public void NullWhitelist_NotAllowed()
    {
        Assert.False(PathValidator.IsAllowed(@"C:\Docs\a.png", null!));
    }

    [Fact]
    public void WhitelistWithNullOrEmptyEntries_StillWorks()
    {
        var wl = new[] { null!, "", "   ", @"C:\Docs" };
        Assert.True(PathValidator.IsAllowed(@"C:\Docs\a.png", wl));
        Assert.False(PathValidator.IsAllowed(@"C:\Windows\evil.dll", wl));
    }

    [Fact]
    public void WhitelistWithMalformedEntry_FailsClosedNotThrows()
    {
        var wl = new[] { "::invalid::", @"C:\Docs" };
        Assert.True(PathValidator.IsAllowed(@"C:\Docs\a.png", wl));
        Assert.False(PathValidator.IsAllowed(@"C:\Windows\evil.dll", wl));
    }

    [Fact]
    public void Whitelist_TrailingSeparator_Works()
    {
        var wl = new[] { @"C:\Docs\" };
        Assert.True(PathValidator.IsAllowed(@"C:\Docs\a.png", wl));
    }

    [Theory]
    [InlineData(@"C:\DocsExtra\foo.png", false)]   // prefix-collision protection
    [InlineData(@"C:/Docs/images/a.png", true)]    // forward-slash on Windows
    [InlineData(@"\\?\C:\Docs\..\Windows\x.png", false)]   // DOS-device + traversal
    public void IsAllowed_EdgeCases(string path, bool expected)
    {
        Assert.Equal(expected, PathValidator.IsAllowed(path, Whitelist));
    }
}
