using System;
using System.IO;

namespace MarkdownReader.Settings;

public static class AppPaths
{
    public static string LocalRoot { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MarkdownReader");
    public static string SettingsFile => Path.Combine(LocalRoot, "settings.json");
    public static string CacheDir   => Path.Combine(LocalRoot, "image-cache");
    public static string LogFile    => Path.Combine(LocalRoot, "log.txt");
}
