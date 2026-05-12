using System;
using Microsoft.Win32;

namespace MarkdownReader.Theme;

public sealed class SystemThemeWatcher : IDisposable
{
    public event Action<bool>? IsLightChanged;
    public bool IsLight => ReadIsLight();

    public SystemThemeWatcher()
        => SystemEvents.UserPreferenceChanged += OnPrefChanged;

    private void OnPrefChanged(object? s, UserPreferenceChangedEventArgs e)
    {
        if (e.Category == UserPreferenceCategory.General)
            IsLightChanged?.Invoke(ReadIsLight());
    }

    private static bool ReadIsLight()
    {
        try
        {
            using var k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return (k?.GetValue("AppsUseLightTheme") as int?) != 0;
        }
        catch { return true; }
    }

    public void Dispose() => SystemEvents.UserPreferenceChanged -= OnPrefChanged;
}
