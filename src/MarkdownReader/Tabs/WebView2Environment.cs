using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Web.WebView2.Core;
using MarkdownReader.Diagnostics;
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
        try
        {
            var udf = Path.Combine(AppPaths.LocalRoot, "WebView2");
            Directory.CreateDirectory(udf);

            // Custom scheme registration for mdimg:// (Task 3.6/3.7 uses this).
            // CAREFUL: CoreWebView2EnvironmentOptions's parameterless constructor
            // actually calls the 5-param overload with customSchemeRegistrations: null
            // as default. The property CustomSchemeRegistrations is get-only — so
            // we must pass the list via the constructor, not assign after.
            var scheme = new CoreWebView2CustomSchemeRegistration("mdimg")
            {
                TreatAsSecure = true,
                HasAuthorityComponent = true
            };
            scheme.AllowedOrigins.Add("*");

            var opts = new CoreWebView2EnvironmentOptions(
                additionalBrowserArguments: null,
                language: null,
                targetCompatibleBrowserVersion: null,
                allowSingleSignOnUsingOSPrimaryAccount: false,
                customSchemeRegistrations: new List<CoreWebView2CustomSchemeRegistration> { scheme }
            );

            var env = await CoreWebView2Environment.CreateAsync(null, udf, opts);
            lock (_lock) { _env = env; _initTask = null; }
            return env;
        }
        catch (Exception ex)
        {
            AppLogger.Error("WebView2Environment.InitAsync", ex);
            lock (_lock) { _initTask = null; }
            throw;
        }
    }
}
