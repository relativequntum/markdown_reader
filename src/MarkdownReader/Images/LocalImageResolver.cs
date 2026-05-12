using System;
using System.IO;
using System.Threading.Tasks;

namespace MarkdownReader.Images;

public sealed class LocalImageResolver
{
    private readonly Func<string[]> _whitelistProvider;

    public LocalImageResolver(Func<string[]> whitelistProvider)
        => _whitelistProvider = whitelistProvider;

    public Task<(byte[] Bytes, string ContentType)?> ResolveLocalAsync(string baseDir, string relPath)
    {
        string full;
        try { full = Path.GetFullPath(Path.Combine(baseDir, relPath)); }
        catch { return Task.FromResult<(byte[], string)?>(null); }
        return ResolveAsync(full);
    }

    public Task<(byte[] Bytes, string ContentType)?> ResolveAbsAsync(string absOrFileUrl)
    {
        string path;
        try
        {
            path = absOrFileUrl.StartsWith("file://", StringComparison.OrdinalIgnoreCase)
                ? new Uri(absOrFileUrl).LocalPath
                : absOrFileUrl;
        }
        catch { return Task.FromResult<(byte[], string)?>(null); }
        return ResolveAsync(path);
    }

    private async Task<(byte[] Bytes, string ContentType)?> ResolveAsync(string path)
    {
        if (!PathValidator.IsAllowed(path, _whitelistProvider())) return null;
        if (!File.Exists(path)) return null;
        try
        {
            await using var fs = new FileStream(path, FileMode.Open, FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete, 4096, FileOptions.SequentialScan | FileOptions.Asynchronous);
            var bytes = new byte[fs.Length];
            int off = 0;
            while (off < bytes.Length)
            {
                var n = await fs.ReadAsync(bytes.AsMemory(off));
                if (n == 0) break;
                off += n;
            }
            return (bytes, ContentTypeMap.FromPath(path));
        }
        catch { return null; }
    }
}
