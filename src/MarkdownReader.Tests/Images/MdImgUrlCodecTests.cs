using MarkdownReader.Images;
using Xunit;

namespace MarkdownReader.Tests.Images;

public class MdImgUrlCodecTests
{
    [Fact]
    public void EncodeLocal_RoundTrips()
    {
        var url = MdImgUrlCodec.EncodeLocal(@"images\foo.png", @"C:\Docs\my note");
        var decoded = MdImgUrlCodec.Decode(url);
        Assert.Equal(MdImgKind.Local, decoded.Kind);
        Assert.Equal(@"images\foo.png", decoded.Payload);
        Assert.Equal(@"C:\Docs\my note", decoded.BaseDir);
    }

    [Fact]
    public void EncodeRemote_RoundTrips()
    {
        var url = MdImgUrlCodec.EncodeRemote("https://i.imgur.com/abc.png?x=1");
        var decoded = MdImgUrlCodec.Decode(url);
        Assert.Equal(MdImgKind.Remote, decoded.Kind);
        Assert.Equal("https://i.imgur.com/abc.png?x=1", decoded.Payload);
    }

    [Fact]
    public void EncodeAbs_RoundTrips()
    {
        var url = MdImgUrlCodec.EncodeAbs(@"D:\pics\汉字 + 空格.jpg");
        var decoded = MdImgUrlCodec.Decode(url);
        Assert.Equal(MdImgKind.Abs, decoded.Kind);
        Assert.Equal(@"D:\pics\汉字 + 空格.jpg", decoded.Payload);
    }

    [Theory]
    [InlineData("mdimg://")]
    [InlineData("mdimg://unknown/abc")]
    [InlineData("http://x/y")]
    [InlineData("mdimg://local/!@#$%")]   // 非法 b64u
    public void Decode_Invalid_Throws(string s)
    {
        Assert.Throws<FormatException>(() => MdImgUrlCodec.Decode(s));
    }

    [Fact]
    public void Base64Url_NoPlusSlashEquals()
    {
        // 构造一个会触发 +/= 的输入
        var url = MdImgUrlCodec.EncodeRemote(new string('?', 100));
        Assert.DoesNotContain('+', url);
        Assert.DoesNotContain('/', url[("mdimg://remote/".Length)..]);
        Assert.DoesNotContain('=', url);
    }
}
