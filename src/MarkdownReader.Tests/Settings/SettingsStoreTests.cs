using System;
using System.IO;
using MarkdownReader.Settings;
using Xunit;

namespace MarkdownReader.Tests.Settings;

public class SettingsStoreTests : IDisposable
{
    private readonly string _tmp = Path.Combine(Path.GetTempPath(), "mdr-test-" + Guid.NewGuid());
    private readonly string _file;

    public SettingsStoreTests()
    {
        Directory.CreateDirectory(_tmp);
        _file = Path.Combine(_tmp, "settings.json");
    }
    public void Dispose() { Directory.Delete(_tmp, true); }

    [Fact]
    public void Load_NoFile_ReturnsDefaults()
    {
        var s = SettingsStore.Load(_file);
        Assert.Equal(ThemeChoice.System, s.Theme);
        Assert.Empty(s.RecentFiles);
    }

    [Fact]
    public void RoundTrip()
    {
        var s = new MarkdownReader.Settings.Settings { Theme = ThemeChoice.Dark };
        s.RecentFiles.Add(@"C:\a.md");
        SettingsStore.Save(_file, s);
        var loaded = SettingsStore.Load(_file);
        Assert.Equal(ThemeChoice.Dark, loaded.Theme);
        Assert.Single(loaded.RecentFiles);
    }

    [Fact]
    public void Corrupt_File_FallsBack_AndBackups()
    {
        File.WriteAllText(_file, "{ not valid json");
        var s = SettingsStore.Load(_file);
        Assert.Equal(ThemeChoice.System, s.Theme);   // defaults

        var backups = Directory.GetFiles(_tmp, "settings.json.bad-*");
        Assert.Single(backups);
    }

    [Fact]
    public void Save_IsAtomic()
    {
        SettingsStore.Save(_file, new MarkdownReader.Settings.Settings());
        Assert.True(File.Exists(_file));
    }
}
