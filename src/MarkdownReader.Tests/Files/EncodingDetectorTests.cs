using System.Text;
using MarkdownReader.Files;
using Xunit;

namespace MarkdownReader.Tests.Files;

public class EncodingDetectorTests
{
    [Fact]
    public void Utf8Bom() => AssertDetect(new byte[]{0xEF,0xBB,0xBF,0x68,0x69}, "UTF-8", "hi");

    [Fact]
    public void Utf16LeBom() => AssertDetect(new byte[]{0xFF,0xFE,0x68,0x00,0x69,0x00}, "UTF-16", "hi");

    [Fact]
    public void Utf16BeBom() => AssertDetect(new byte[]{0xFE,0xFF,0x00,0x68,0x00,0x69}, "UTF-16BE", "hi");

    [Fact]
    public void PureAscii_AsUtf8()
    {
        AssertDetect(Encoding.ASCII.GetBytes("hello world"), "UTF-8", "hello world");
    }

    [Fact]
    public void Utf8Chinese_NoBom()
    {
        var bytes = Encoding.UTF8.GetBytes("中文测试");
        AssertDetect(bytes, "UTF-8", "中文测试");
    }

    [Fact]
    public void GbkChinese_FallsBackToAnsi()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var gbk = Encoding.GetEncoding(936);
        var bytes = gbk.GetBytes("中文测试");
        var (enc, text) = EncodingDetector.DetectAndDecode(bytes);
        Assert.NotEqual("UTF-8", enc.WebName.ToUpperInvariant());
        Assert.Equal("中文测试", text);
    }

    private static void AssertDetect(byte[] bytes, string expectedWebPrefix, string expectedText)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var (enc, text) = EncodingDetector.DetectAndDecode(bytes);
        Assert.StartsWith(expectedWebPrefix, enc.WebName, System.StringComparison.OrdinalIgnoreCase);
        Assert.Equal(expectedText, text);
    }
}
