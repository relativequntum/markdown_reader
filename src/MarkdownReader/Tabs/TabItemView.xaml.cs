using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Controls;
using Microsoft.Web.WebView2.Core;

namespace MarkdownReader.Tabs;

public partial class TabItemView : UserControl
{
    public TabState State { get; } = new();
    public event Action<string>? HeaderTextChanged;
    public event Action? RequestClose;

    private TaskCompletionSource _webReady = new();

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

            Web.CoreWebView2.WebMessageReceived += OnWebMessage;
            Web.Source = new Uri("https://app.viewer/index.html");
        }
        catch (Exception ex)
        {
            // Placeholder: future Task 4.1 ErrorBanner will surface this nicely.
            BannerHost.Content = new TextBlock
            {
                Text = $"WebView2 init failed: {ex.Message}",
                Padding = new System.Windows.Thickness(10),
                Background = System.Windows.Media.Brushes.MistyRose
            };
        }
    }

    public void LoadFile(string path)
    {
        State.FilePath = path;
        State.BaseDir = Path.GetDirectoryName(path) ?? "";
        HeaderTextChanged?.Invoke(Path.GetFileName(path));
        // Task 3.5 will: read file, wait for _webReady, post render message
    }

    private void OnWebMessage(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(e.WebMessageAsJson);
            if (!doc.RootElement.TryGetProperty("type", out var typeProp)) return;
            var type = typeProp.GetString();
            switch (type)
            {
                case "ready":
                    _webReady.TrySetResult();
                    break;
                // linkClick / rendered / error handlers come in Tasks 3.5/4.x
            }
        }
        catch (System.Text.Json.JsonException) { /* ignore malformed */ }
    }
}
