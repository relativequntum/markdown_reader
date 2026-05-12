using System;
using System.IO;
using System.Linq;
using System.Security.Principal;
using System.Threading;
using MarkdownReader.SingleInstance;

namespace MarkdownReader;

public static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        var sid = WindowsIdentity.GetCurrent().User?.Value ?? "default";
        var mutexName = $@"Local\MarkdownReader.SingleInstance.{sid}";
        var pipeName  = $"MarkdownReader.OpenFile.{sid}";

        var path = args.FirstOrDefault(a => File.Exists(a));

        var mutex = new Mutex(initiallyOwned: true, mutexName, out var createdNew);
        if (!createdNew)
        {
            mutex.Dispose();
            var ok = PipeClient.Send(pipeName, path is null
                ? SingleInstanceProtocol.EncodeFocus()
                : SingleInstanceProtocol.EncodeOpen(path), timeoutMs: 500);

            if (ok) return 0;
            // 主实例可能僵死：等 1s 再抢
            Thread.Sleep(1000);
            mutex = new Mutex(initiallyOwned: true, mutexName, out var got);
            if (!got) { mutex.Dispose(); return 1; }
            return RunMain(mutex, pipeName, path);
        }
        return RunMain(mutex, pipeName, path);
    }

    private static int RunMain(Mutex mutex, string pipeName, string? initialPath)
    {
        try
        {
            var app = new App();
            // App.xaml currently has no loadable XAML content, so WPF does
            // not generate InitializeComponent. When resources/dictionaries
            // are added later, restore: app.InitializeComponent();
            app.PipeName = pipeName;
            app.InitialPath = initialPath;
            return app.Run();
        }
        finally
        {
            try { mutex.ReleaseMutex(); } catch { /* ignore */ }
            mutex.Dispose();
        }
    }
}
