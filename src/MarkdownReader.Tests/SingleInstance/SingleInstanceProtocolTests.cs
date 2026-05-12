using MarkdownReader.SingleInstance;
using Xunit;

namespace MarkdownReader.Tests.SingleInstance;

public class SingleInstanceProtocolTests
{
    [Fact]
    public void Open_Encode_Decode()
    {
        var msg = SingleInstanceProtocol.EncodeOpen(@"C:\Docs\a.md");
        var decoded = SingleInstanceProtocol.Decode(msg);
        Assert.IsType<OpenMessage>(decoded);
        Assert.Equal(@"C:\Docs\a.md", ((OpenMessage)decoded).Path);
    }

    [Fact]
    public void Focus_Encode_Decode()
    {
        var msg = SingleInstanceProtocol.EncodeFocus();
        Assert.IsType<FocusMessage>(SingleInstanceProtocol.Decode(msg));
    }

    [Theory]
    [InlineData(@"C:\Docs\a.md")]
    [InlineData(@"D:\some path\file.md")]
    public void BareLine_FallsBackToOpen(string path)
    {
        var decoded = SingleInstanceProtocol.Decode(path + "\n");
        Assert.IsType<OpenMessage>(decoded);
        Assert.Equal(path, ((OpenMessage)decoded).Path);
    }

    [Fact]
    public void Empty_Returns_Null()
    {
        Assert.Null(SingleInstanceProtocol.Decode(""));
        Assert.Null(SingleInstanceProtocol.Decode("\n"));
    }
}
