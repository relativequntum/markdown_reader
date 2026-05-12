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
        if (!IsWebView2Available())
        {
            ShowMissingWebView2Dialog();
            return 2;
        }

        var sid = WindowsIdentity.GetCurrent().User?.Value ?? "default";
        var mutexName = $@"Local\MarkdownReader.SingleInstance.{sid}";
        var pipeName  = $"MarkdownReader.OpenFile.{sid}";

        // First positional arg is the file path. Don't filter on File.Exists —
        // pass the path through so TabItemView.LoadFile can surface a banner
        // ("找不到文件: ..." with "从最近列表移除" action) for stale recent files.
        var path = args.FirstOrDefault(a => !a.StartsWith("-", StringComparison.Ordinal));

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

    private static bool IsWebView2Available()
    {
        try
        {
            var ver = Microsoft.Web.WebView2.Core.CoreWebView2Environment.GetAvailableBrowserVersionString();
            return !string.IsNullOrEmpty(ver);
        }
        catch { return false; }
    }

    private static void ShowMissingWebView2Dialog()
    {
        var r = System.Windows.MessageBox.Show(
            "本程序需要 Microsoft Edge WebView2 Runtime。\n\n是否打开下载页？",
            "缺少 WebView2 Runtime",
            System.Windows.MessageBoxButton.OKCancel,
            System.Windows.MessageBoxImage.Warning);
        if (r == System.Windows.MessageBoxResult.OK)
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(
                    "https://developer.microsoft.com/microsoft-edge/webview2/")
                { UseShellExecute = true });
            }
            catch { /* best effort */ }
        }
    }
}
