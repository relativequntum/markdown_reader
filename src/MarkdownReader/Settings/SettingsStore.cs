using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MarkdownReader.Settings;

public static class SettingsStore
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static AppSettings Load(string path)
    {
        try
        {
            if (!File.Exists(path)) return new AppSettings();
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<AppSettings>(json, JsonOpts) ?? new AppSettings();
        }
        catch (JsonException)
        {
            var dir = Path.GetDirectoryName(path)!;
            var ts = DateTime.UtcNow.ToString("yyyyMMddHHmmssfff");
            var bad = Path.Combine(dir, $"settings.json.bad-{ts}");
            try { File.Move(path, bad, overwrite: true); } catch { /* best effort */ }
            return new AppSettings();
        }
    }

    public static void Save(string path, AppSettings settings)
    {
        var dir = Path.GetDirectoryName(path)!;
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(settings, JsonOpts));
        if (File.Exists(path)) File.Replace(tmp, path, destinationBackupFileName: null);
        else File.Move(tmp, path);
    }
}
