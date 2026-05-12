using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using MarkdownReader.Diagnostics;
using MarkdownReader.Files;
using MarkdownReader.Images;
using MarkdownReader.Settings;
using Microsoft.Web.WebView2.Core;

namespace MarkdownReader.Tabs;

public partial class TabItemView : UserControl
{
    public TabState State { get; } = new();
    public event Action<string>? HeaderTextChanged;
    public event Action? RequestClose;

    private const long WarnThresholdBytes = 8L * 1024 * 1024;
    private const long RejectThresholdBytes = 50L * 1024 * 1024;

    private TaskCompletionSource _webReady = new();
    private FileWatcher? _watcher;
    private MdImgHandler? _imgHandler;

    public TabItemView()
    {
        InitializeComponent();
        Loaded += async (_, _) => await InitWebViewAsync();
    }

    private async Task InitWebViewAsync()
    {
        try
        {
            var env = await WebView2Environment.GetAsync();
            await Web.EnsureCoreWebView2Async(env);

            var exeDir = Path.GetDirectoryName(Environment.ProcessPath)
                         ?? AppDomain.CurrentDomain.BaseDirectory;
            var viewerRoot = Path.Combine(exeDir, "Resources", "viewer");
            if (!Directory.Exists(viewerRoot))
                throw new DirectoryNotFoundException(
                    $"viewer assets not found at: {viewerRoot}");

            Web.CoreWebView2.SetVirtualHostNameToFolderMapping(
                "app.viewer", viewerRoot, CoreWebView2HostResourceAccessKind.Allow);

            // Register mdimg:// custom-scheme handler. Whitelist is evaluated per-request,
            // so it sees the current State.BaseDir without re-registering when LoadFile runs.
            var resolver = new LocalImageResolver(() =>
            {
                var settings = ((App)Application.Current).Settings;
                var roots = new List<string>
                {
                    State.BaseDir,
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    Path.GetTempPath()
                };
                roots.AddRange(settings.ImagePathWhitelist);
                return roots
                    .Where(p => !string.IsNullOrWhiteSpace(p))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();
            });
            _imgHandler = new MdImgHandler(resolver, () => env);
            var app = (App)Application.Current;
            if (app.Fetcher != null) _imgHandler.RemoteFetcher = app.Fetcher.FetchAsync;
            _imgHandler.Register(Web.CoreWebView2);

            Web.CoreWebView2.WebMessageReceived += OnWebMessage;
            Web.CoreWebView2.ProcessFailed += OnProcessFailed;

            // Diagnostic: enable F12 dev tools and log navigation outcomes so we
            // can see why a blank page might not render.
            Web.CoreWebView2.Settings.AreDevToolsEnabled = true;
            Web.CoreWebView2.NavigationCompleted += (_, args) =>
            {
                if (!args.IsSuccess)
                    AppLogger.Error("Navigation failed",
                        new Exception($"WebErrorStatus={args.WebErrorStatus}, HttpStatusCode={args.HttpStatusCode}"));
            };
            Web.CoreWebView2.WebResourceResponseReceived += (_, args) =>
            {
                var status = args.Response?.StatusCode ?? 0;
                if (status >= 400)
                {
                    AppLogger.Error("Resource fetch failed",
                        new Exception($"{status} {args.Response?.ReasonPhrase}: {args.Request.Uri}"));
                }
            };

            Web.Source = new Uri("https://app.viewer/index.html");
        }
        catch (Exception ex)
        {
            AppLogger.Error("WebView2 init", ex);
            ShowBanner("error",
                $"WebView2 初始化失败: {ex.GetType().Name} — {ex.Message}. " +
                $"日志: %LocalAppData%\\MarkdownReader\\log.txt");
        }
    }

    public async void LoadFile(string path)
    {
        State.FilePath = path;
        State.BaseDir = Path.GetDirectoryName(path) ?? "";
        HeaderTextChanged?.Invoke(Path.GetFileName(path));

        long size;
        try { size = new FileInfo(path).Length; }
        catch (Exception ex) { ShowBanner("error", $"无法读取文件: {ex.Message}"); return; }

        if (size > RejectThresholdBytes)
        {
            ShowBanner("error", $"文件过大 ({size / 1024 / 1024} MB)，可能不是文本");
            return;
        }
        if (size > WarnThresholdBytes)
        {
            var sizeMB = (size / 1024.0 / 1024.0).ToString("F1");
            ShowBanner("warn",
                $"此文件较大 ({sizeMB} MB)，渲染可能需要几秒",
                ("继续渲染", () => { HideBanner(); _ = DoLoadAsync(path); }),
                ("关闭标签页", () => RequestClose?.Invoke()));
            return;
        }

        await DoLoadAsync(path);
    }

    private async Task DoLoadAsync(string path)
    {
        try
        {
            var (text, _, _) = await FileLoader.LoadAsync(path);
            State.RawText = text;
            State.LoadedAt = DateTime.UtcNow;
            await PostRenderAsync();
        }
        catch (FileNotFoundException)
        {
            ShowBanner("error", $"找不到文件: {path}",
                ("从最近列表移除", () => RemoveFromRecent(path)));
        }
        catch (DirectoryNotFoundException) { ShowBanner("error", $"找不到目录: {path}"); }
        catch (UnauthorizedAccessException) { ShowBanner("error", "无权访问该文件"); }
        catch (IOException ex) { ShowBanner("error", ex.Message); }

        // Watch for external changes
        _watcher?.Dispose();
        try
        {
            _watcher = new FileWatcher(path, TimeSpan.FromMilliseconds(200));
            // Dispatcher.InvokeAsync(async () => …) is async-void under the hood — acceptable here
            // because we want fire-and-forget UI marshaling; ReloadAsync swallows transient errors.
            _watcher.Changed += () => Dispatcher.InvokeAsync(async () => await ReloadAsync());
            _watcher.Renamed += np => Dispatcher.InvokeAsync(() =>
            {
                State.FilePath = np;
                State.BaseDir = Path.GetDirectoryName(np) ?? "";
                HeaderTextChanged?.Invoke(Path.GetFileName(np));
            });
            _watcher.Deleted += () => Dispatcher.InvokeAsync(() =>
            {
                State.IsDeleted = true;
                ShowBanner("warn", "⚠ 文件已被删除（最后一次内容仍在显示）");
            });
        }
        catch { /* FileSystemWatcher can fail on some drives (e.g. network), continue without */ }
    }

    private async Task ReloadAsync()
    {
        if (State.IsDeleted) return;
        try
        {
            var (text, _, _) = await FileLoader.LoadAsync(State.FilePath);
            State.RawText = text;
            State.LoadedAt = DateTime.UtcNow;
            await PostRenderAsync();
        }
        catch { /* silent — file may be in the middle of being written */ }
    }

    private async Task PostRenderAsync()
    {
        await _webReady.Task;
        if (Web.CoreWebView2 == null) return;
        var theme = ((App)Application.Current).Settings.Theme switch
        {
            ThemeChoice.Dark => "dark",
            ThemeChoice.Light => "light",
            _ => SystemTheme()
        };
        var payload = JsonSerializer.Serialize(new
        {
            type = "render",
            md = State.RawText ?? "",
            baseDir = State.BaseDir,
            theme
        });
        Web.CoreWebView2.PostWebMessageAsJson(payload);
    }

    public void PushTheme(ThemeChoice t)
    {
        if (Web.CoreWebView2 == null) return;
        var theme = t switch
        {
            ThemeChoice.Dark => "dark",
            ThemeChoice.Light => "light",
            _ => SystemTheme()
        };
        var payload = System.Text.Json.JsonSerializer.Serialize(new { type = "setTheme", theme });
        Web.CoreWebView2.PostWebMessageAsJson(payload);
    }

    private static string SystemTheme()
    {
        var app = (App)System.Windows.Application.Current;
        return app.ThemeWatcher?.IsLight == false ? "dark" : "light";
    }

    private async void OnProcessFailed(object? sender, CoreWebView2ProcessFailedEventArgs e)
    {
        var kind = e.ProcessFailedKind;
        if (kind is not (CoreWebView2ProcessFailedKind.RenderProcessExited
                      or CoreWebView2ProcessFailedKind.RenderProcessUnresponsive))
            return;

        Banner.Show("warn", "渲染进程异常，正在恢复…");

        try
        {
            // Reload current page; WebView2 reuses the same control, the render process spawns fresh
            if (Web.CoreWebView2 != null)
            {
                Web.CoreWebView2.Reload();
                _webReady = new TaskCompletionSource();
                await _webReady.Task;

                // Re-post the render message so the doc shows up
                if (State.RawText != null) await PostRenderAsync();
            }
            Banner.Hide();
        }
        catch (Exception ex)
        {
            Banner.Show("error", $"恢复失败: {ex.Message}");
        }
    }

    private void OnWebMessage(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        try
        {
            using var doc = JsonDocument.Parse(e.WebMessageAsJson);
            if (!doc.RootElement.TryGetProperty("type", out var typeProp)) return;
            var type = typeProp.GetString();
            switch (type)
            {
                case "ready":
                    _webReady.TrySetResult();
                    break;
                case "linkClick":
                    HandleLinkClick(doc.RootElement);
                    break;
                case "error":
                    if (doc.RootElement.TryGetProperty("message", out var m))
                        ShowBanner("error", m.GetString() ?? "");
                    break;
                case "rendered":
                    // Performance hook — Task 5.x will use this for benchmarks
                    break;
            }
        }
        catch (JsonException) { /* ignore malformed messages */ }
    }

    private void HandleLinkClick(JsonElement el)
    {
        var href = el.TryGetProperty("href", out var h) ? h.GetString() ?? "" : "";
        var kind = el.TryGetProperty("kind", out var k) ? k.GetString() ?? "" : "";
        try
        {
            if (kind == "external")
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(href) { UseShellExecute = true });
            }
            else if (kind == "mdfile")
            {
                var resolved = Path.IsPathRooted(href) ? href : Path.GetFullPath(Path.Combine(State.BaseDir, href));
                if (Application.Current.MainWindow is MarkdownReader.MainWindow mw)
                    mw.OpenFile(resolved);
            }
            else if (kind == "localfile")
            {
                var r = MessageBox.Show($"用系统默认程序打开:\n{href}?", "确认", MessageBoxButton.OKCancel);
                if (r == MessageBoxResult.OK)
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(href) { UseShellExecute = true });
            }
            // anchor handled by browser default; invalid neutralized at viewer side
        }
        catch (Exception ex)
        {
            ShowBanner("error", $"打开链接失败: {ex.Message}");
        }
    }

    private void ShowBanner(string kind, string text, params (string, Action)[] actions)
        => Banner.Show(kind, text, actions);

    private void HideBanner() => Banner.Hide();

    private static void RemoveFromRecent(string path)
    {
        var app = (App)System.Windows.Application.Current;
        app.Settings.RecentFiles.RemoveAll(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase));
        try { Settings.SettingsStore.Save(Settings.AppPaths.SettingsFile, app.Settings); } catch { }
    }
}
