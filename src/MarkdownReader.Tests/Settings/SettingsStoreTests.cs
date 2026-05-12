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
        var s = new AppSettings { Theme = ThemeChoice.Dark };
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
        Assert.Equal(ThemeChoice.System, s.Theme);

        var backups = Directory.GetFiles(_tmp, "settings.json.bad-*");
        Assert.Single(backups);
    }

    [Fact]
    public void Save_NewFile_Succeeds()
    {
        SettingsStore.Save(_file, new AppSettings());
        Assert.True(File.Exists(_file));
    }

    [Fact]
    public void Save_OverwritesExisting()
    {
        SettingsStore.Save(_file, new AppSettings { Theme = ThemeChoice.Light });
        SettingsStore.Save(_file, new AppSettings { Theme = ThemeChoice.Dark });
        var loaded = SettingsStore.Load(_file);
        Assert.Equal(ThemeChoice.Dark, loaded.Theme);
    }
}
