using System;
using System.IO;
using System.IO.Pipes;
using System.Text;

namespace MarkdownReader.SingleInstance;

public static class PipeClient
{
    public static bool Send(string pipeName, string message, int timeoutMs)
    {
        try
        {
            using var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.Out, PipeOptions.CurrentUserOnly);
            pipe.Connect(timeoutMs);
            var bytes = Encoding.UTF8.GetBytes(message);
            pipe.Write(bytes, 0, bytes.Length);
            pipe.Flush();
            return true;
        }
        catch (TimeoutException) { return false; }
        catch (IOException) { return false; }
        catch (UnauthorizedAccessException) { return false; }
    }
}
