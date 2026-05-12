using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Web.WebView2.Core;
using MarkdownReader.Settings;

namespace MarkdownReader.Tabs;

public static class WebView2Environment
{
    private static CoreWebView2Environment? _env;
    private static Task<CoreWebView2Environment>? _initTask;
    private static readonly object _lock = new();

    public static Task<CoreWebView2Environment> GetAsync()
    {
        lock (_lock)
        {
            if (_env != null) return Task.FromResult(_env);
            return _initTask ??= InitAsync();
        }
    }

    private static async Task<CoreWebView2Environment> InitAsync()
    {
        var udf = Path.Combine(AppPaths.LocalRoot, "WebView2");
        Directory.CreateDirectory(udf);

        var opts = new CoreWebView2EnvironmentOptions();
        // Custom scheme registration for mdimg:// (Task 3.6/3.7 uses this)
        var scheme = new CoreWebView2CustomSchemeRegistration("mdimg")
        {
            TreatAsSecure = true,
            HasAuthorityComponent = true
        };
        scheme.AllowedOrigins.Add("*");
        opts.CustomSchemeRegistrations.Add(scheme);

        var env = await CoreWebView2Environment.CreateAsync(null, udf, opts);
        lock (_lock) { _env = env; _initTask = null; }
        return env;
    }
}
