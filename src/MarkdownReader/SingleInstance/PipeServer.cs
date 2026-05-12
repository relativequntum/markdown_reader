using System;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MarkdownReader.SingleInstance;

public sealed class PipeServer : IDisposable
{
    private readonly string _name;
    private readonly Action<IpcMessage> _onMessage;
    private readonly CancellationTokenSource _cts = new();
    private Task? _loop;

    public PipeServer(string name, Action<IpcMessage> onMessage)
    {
        _name = name;
        _onMessage = onMessage;
    }

    public void Start() => _loop = Task.Run(() => Loop(_cts.Token));

    private async Task Loop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                using var pipe = new NamedPipeServerStream(
                    _name, PipeDirection.In, maxNumberOfServerInstances: 1,
                    PipeTransmissionMode.Byte,
                    PipeOptions.CurrentUserOnly | PipeOptions.Asynchronous);
                await pipe.WaitForConnectionAsync(ct);

                using var ms = new MemoryStream();
                var buf = new byte[4096];
                int n;
                while ((n = await pipe.ReadAsync(buf.AsMemory(), ct)) > 0) ms.Write(buf, 0, n);
                var text = Encoding.UTF8.GetString(ms.ToArray());
                var msg = SingleInstanceProtocol.Decode(text);
                if (msg is not null)
                {
                    try { _onMessage(msg); }
                    catch { /* user handler exceptions don't kill the loop */ }
                }
            }
            catch (OperationCanceledException) { break; }
            catch (IOException) { /* client disconnected mid-read; continue */ }
            catch (UnauthorizedAccessException) { /* cross-user attempt; continue */ }
        }
    }

    public void Dispose()
    {
        _cts.Cancel();
        try { _loop?.Wait(TimeSpan.FromSeconds(1)); } catch { }
        _cts.Dispose();
    }
}
