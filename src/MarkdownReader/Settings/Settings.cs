using System.Collections.Generic;

namespace MarkdownReader.Settings;

public enum ThemeChoice { System, Light, Dark }

public sealed class Settings
{
    public ThemeChoice Theme { get; set; } = ThemeChoice.System;
    public long ImageCacheMaxBytes { get; set; } = 500L * 1024 * 1024;
    public int ImageCacheMaxFiles { get; set; } = 5000;
    public List<string> RecentFiles { get; set; } = new();
    public List<string> ImagePathWhitelist { get; set; } = new();
    public int MaxRecent { get; set; } = 20;
}
