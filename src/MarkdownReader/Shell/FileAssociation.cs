using System;
using System.Diagnostics;
using Microsoft.Win32;

namespace MarkdownReader.Shell;

public static class FileAssociation
{
    private const string ProgId = "MarkdownReader.Document";

    public static bool IsRegistered()
    {
        using var ext = Registry.CurrentUser.OpenSubKey(@"Software\Classes\.md");
        return ext?.GetValue(null) as string == ProgId;
    }

    public static void Register()
    {
        var exe = Process.GetCurrentProcess().MainModule?.FileName
                  ?? throw new InvalidOperationException("Cannot determine exe path");

        using (var ext = Registry.CurrentUser.CreateSubKey(@"Software\Classes\.md"))
            ext.SetValue(null, ProgId);

        using (var prog = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{ProgId}"))
            prog.SetValue(null, "Markdown Document");

        using (var cmd = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{ProgId}\shell\open\command"))
            cmd.SetValue(null, $"\"{exe}\" \"%1\"");
    }

    public static void Unregister()
    {
        try { Registry.CurrentUser.DeleteSubKeyTree(@"Software\Classes\.md", throwOnMissingSubKey: false); } catch { }
        try { Registry.CurrentUser.DeleteSubKeyTree($@"Software\Classes\{ProgId}", throwOnMissingSubKey: false); } catch { }
    }
}
