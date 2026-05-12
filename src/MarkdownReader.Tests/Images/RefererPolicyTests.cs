using MarkdownReader.Images;
using Xunit;

namespace MarkdownReader.Tests.Images;

public class RefererPolicyTests
{
    [Theory]
    [InlineData("https://i.imgur.com/foo.png", "https://i.imgur.com/")]
    [InlineData("https://raw.githubusercontent.com/u/r/m/x.jpg", "https://raw.githubusercontent.com/")]
    [InlineData("http://example.com:8080/a/b/c.gif", "http://example.com:8080/")]
    public void OriginReferer(string url, string expected)
    {
        Assert.Equal(expected, RefererPolicy.OriginOf(url));
    }

    [Theory]
    [InlineData("not a url")]
    [InlineData("ftp://x/y")]
    public void OriginReferer_InvalidReturnsNull(string url)
    {
        Assert.Null(RefererPolicy.OriginOf(url));
    }
}
