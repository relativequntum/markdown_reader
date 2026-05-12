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
}
