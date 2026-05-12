using System;
using System.IO;
using System.Text;
using MarkdownReader.Settings;

namespace MarkdownReader.Diagnostics;

/// <summary>
/// Best-effort file logger at %LocalAppData%\MarkdownReader\log.txt.
/// Swallows all exceptions — logging must never crash the app.
/// </summary>
public static class AppLogger
{
    private static readonly object _lock = new();

    public static void Error(string context, Exception ex)
    {
        try
        {
            Directory.CreateDirectory(AppPaths.LocalRoot);
            var line = new StringBuilder()
                .Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"))
                .Append(" [ERROR] ")
                .Append(context)
                .Append(": ")
                .Append(ex.GetType().Name)
                .Append(" — ")
                .Append(ex.Message)
                .AppendLine()
                .Append(ex.ToString())
                .AppendLine("---");
            lock (_lock)
            {
                File.AppendAllText(AppPaths.LogFile, line.ToString(), Encoding.UTF8);
            }
        }
        catch { /* never throw from a logger */ }
    }
}
