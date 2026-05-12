using System.Windows;
using MarkdownReader.Settings;
using MarkdownReader.SingleInstance;

namespace MarkdownReader;

public partial class App : Application
{
    public string PipeName { get; set; } = "";
    public string? InitialPath { get; set; }
    public AppSettings Settings { get; private set; } = new();
    public PipeServer? PipeServer { get; private set; }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        Settings = SettingsStore.Load(AppPaths.SettingsFile);

        // Start IPC server first so a second instance launching during
        // MainWindow construction can connect immediately.
        PipeServer = new PipeServer(PipeName, OnIpc);
        PipeServer.Start();

        var mw = new MainWindow();
        MainWindow = mw;
        mw.Show();
        if (InitialPath is not null) mw.OpenFile(InitialPath);
    }

    private void OnIpc(IpcMessage msg)
    {
        Dispatcher.InvokeAsync(() =>
        {
            if (MainWindow is not MainWindow mw) return;
            if (msg is OpenMessage op) mw.OpenFile(op.Path);
            mw.BringToForeground();
        });
    }

    protected override void OnExit(ExitEventArgs e)
    {
        PipeServer?.Dispose();
        try { SettingsStore.Save(AppPaths.SettingsFile, Settings); } catch { }
        base.OnExit(e);
    }
}
