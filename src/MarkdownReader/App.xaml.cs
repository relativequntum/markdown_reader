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
    public Images.ImageCache Cache { get; private set; } = null!;
    public Images.RemoteImageFetcher Fetcher { get; private set; } = null!;
    public Theme.SystemThemeWatcher ThemeWatcher { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        Settings = SettingsStore.Load(AppPaths.SettingsFile);

        // Image cache + remote fetcher (process-wide singletons)
        Cache = new Images.ImageCache(AppPaths.CacheDir, new Files.RealFileSystem(), TimeProvider.System);
        Fetcher = new Images.RemoteImageFetcher(Cache);

        // Async cache cleanup (don't block startup)
        _ = System.Threading.Tasks.Task.Run(() =>
        {
            try { Cache.EnforceLimits(Settings.ImageCacheMaxBytes, Settings.ImageCacheMaxFiles); }
            catch { /* best effort */ }
        });

        // Start IPC server first so a second instance launching during
        // MainWindow construction can connect immediately.
        PipeServer = new PipeServer(PipeName, OnIpc);
        PipeServer.Start();

        var mw = new MainWindow();
        MainWindow = mw;
        mw.Show();
        if (InitialPath is not null) mw.OpenFile(InitialPath);

        ThemeWatcher = new Theme.SystemThemeWatcher();
        ThemeWatcher.IsLightChanged += _ =>
        {
            if (Settings.Theme != ThemeChoice.System) return;
            Dispatcher.InvokeAsync(() =>
            {
                if (MainWindow is MainWindow win)
                {
                    foreach (System.Windows.Controls.TabItem ti in win.Tabs.Items)
                        if (ti.Content is Tabs.TabItemView v)
                            v.PushTheme(ThemeChoice.System);
                }
            });
        };
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
        ThemeWatcher?.Dispose();
        base.OnExit(e);
    }
}
