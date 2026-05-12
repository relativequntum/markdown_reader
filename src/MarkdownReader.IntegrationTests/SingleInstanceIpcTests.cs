using System;
using System.Diagnostics;
using System.Threading;
using MarkdownReader.SingleInstance;
using Xunit;

namespace MarkdownReader.IntegrationTests;

public class SingleInstanceIpcTests
{
    [Fact]
    public void OpenMessage_DeliveredToServer()
    {
        var name = "MarkdownReader.OpenFile.itest." + Guid.NewGuid();
        string? receivedPath = null;
        using var server = new PipeServer(name, m =>
        {
            if (m is OpenMessage op) receivedPath = op.Path;
        });
        server.Start();
        Thread.Sleep(100);   // give the listener time to bind

        var ok = PipeClient.Send(name,
            SingleInstanceProtocol.EncodeOpen(@"C:\Docs\a.md"),
            timeoutMs: 500);
        Assert.True(ok, "PipeClient.Send should succeed");

        // wait for the loop to process the message
        var sw = Stopwatch.StartNew();
        while (receivedPath is null && sw.ElapsedMilliseconds < 2000) Thread.Sleep(20);

        Assert.Equal(@"C:\Docs\a.md", receivedPath);
    }

    [Fact]
    public void FocusMessage_DeliveredToServer()
    {
        var name = "MarkdownReader.OpenFile.itest." + Guid.NewGuid();
        bool focusReceived = false;
        using var server = new PipeServer(name, m =>
        {
            if (m is FocusMessage) focusReceived = true;
        });
        server.Start();
        Thread.Sleep(100);

        var ok = PipeClient.Send(name, SingleInstanceProtocol.EncodeFocus(), timeoutMs: 500);
        Assert.True(ok);

        var sw = Stopwatch.StartNew();
        while (!focusReceived && sw.ElapsedMilliseconds < 2000) Thread.Sleep(20);
        Assert.True(focusReceived);
    }

    [Fact]
    public void Send_DeadServer_Times_Out()
    {
        var name = "MarkdownReader.OpenFile.itest." + Guid.NewGuid();
        // No server bound - Send should time out and return false
        var ok = PipeClient.Send(name, SingleInstanceProtocol.EncodeOpen(@"C:\x.md"), timeoutMs: 200);
        Assert.False(ok);
    }
}
