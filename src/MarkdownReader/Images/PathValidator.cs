using System;
using System.IO;
using System.Linq;

namespace MarkdownReader.Images;

public static class PathValidator
{
    public static bool IsAllowed(string path, string[] whitelist)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;

        string full;
        try { full = Path.GetFullPath(path); }
        catch { return false; }

        // 拒绝 UNC
        if (full.StartsWith(@"\\", StringComparison.Ordinal)) return false;

        return whitelist.Any(root =>
        {
            var rootFull = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar)
                         + Path.DirectorySeparatorChar;
            return full.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase);
        });
    }
}
