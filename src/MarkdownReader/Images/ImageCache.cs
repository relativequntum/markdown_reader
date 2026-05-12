using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MarkdownReader.Files;

namespace MarkdownReader.Images;

public sealed record ImageMeta(string ContentType, string Url, DateTime FetchedAt);

public sealed class ImageCache
{
    private readonly string _root;
    private readonly IFileSystem _fs;
    private readonly TimeProvider _clock;

    public ImageCache(string root, IFileSystem fs, TimeProvider clock)
    {
        _root = root;
        _fs = fs;
        _clock = clock;
        _fs.EnsureDir(_root);
    }

    public bool TryGet(string url, out byte[] bytes, out ImageMeta meta)
    {
        var (binPath, metaPath) = Paths(url);
        if (!_fs.FileExists(binPath) || !_fs.FileExists(metaPath))
        {
            bytes = Array.Empty<byte>();
            meta = default!;
            return false;
        }
        bytes = _fs.ReadAllBytes(binPath);
        meta = JsonSerializer.Deserialize<ImageMeta>(_fs.ReadAllBytes(metaPath))!;
        _fs.SetLastAccessTime(binPath, _clock.GetUtcNow().UtcDateTime);
        return true;
    }

    public void Put(string url, byte[] bytes, string contentType)
    {
        var (binPath, metaPath) = Paths(url);
        var now = _clock.GetUtcNow().UtcDateTime;
        _fs.WriteAllBytes(binPath, bytes);
        var meta = new ImageMeta(contentType, url, now);
        _fs.WriteAllBytes(metaPath, JsonSerializer.SerializeToUtf8Bytes(meta));
        _fs.SetLastAccessTime(binPath, now);
    }

    public void EnforceLimits(long maxBytes, int maxFiles)
    {
        var bins = _fs.EnumerateFiles(_root, "*.bin")
            .Select(p => new { Path = p, Atime = _fs.GetLastAccessTime(p), Size = _fs.GetSize(p) })
            .OrderBy(x => x.Atime)
            .ToList();
        var totalBytes = bins.Sum(b => b.Size);
        var totalFiles = bins.Count;

        foreach (var b in bins)
        {
            if (totalBytes <= maxBytes && totalFiles <= maxFiles) break;
            _fs.Delete(b.Path);
            var metaPath = Path.ChangeExtension(b.Path, ".meta.json");
            _fs.Delete(metaPath);
            totalBytes -= b.Size;
            totalFiles--;
        }
    }

    private (string Bin, string Meta) Paths(string url)
    {
        var hash = Sha256Hex(url);
        var dir = Path.Combine(_root, hash[..2]);
        return (Path.Combine(dir, hash + ".bin"), Path.Combine(dir, hash + ".meta.json"));
    }

    private static string Sha256Hex(string s)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(s));
        var sb = new StringBuilder(bytes.Length * 2);
        foreach (var b in bytes) sb.Append(b.ToString("x2"));
        return sb.ToString();
    }
}
