using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
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

            var viewerRoot = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "viewer");
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
            _imgHandler.RemoteFetcher = app.Fetcher.FetchAsync;
            _imgHandler.Register(Web.CoreWebView2);

            Web.CoreWebView2.WebMessageReceived += OnWebMessage;
            Web.Source = new Uri("https://app.viewer/index.html");
        }
        catch (Exception ex)
        {
            ShowBanner("error", $"WebView2 init failed: {ex.Message}");
        }
    }

    public async void LoadFile(string path)
    {
        State.FilePath = path;
        State.BaseDir = Path.GetDirectoryName(path) ?? "";
        HeaderTextChanged?.Invoke(Path.GetFileName(path));

        try
        {
            var (text, _, _) = await FileLoader.LoadAsync(path);
            State.RawText = text;
            State.LoadedAt = DateTime.UtcNow;
            await PostRenderAsync();
        }
        catch (FileNotFoundException) { ShowBanner("error", $"找不到文件: {path}"); }
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
            _ => "light" // Task 3.9 will inspect SystemThemeWatcher
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

    private void ShowBanner(string kind, string text)
    {
        BannerHost.Content = new TextBlock
        {
            Text = text,
            Padding = new Thickness(10),
            Background = kind == "error" ? Brushes.MistyRose : Brushes.PapayaWhip
        };
    }
}
