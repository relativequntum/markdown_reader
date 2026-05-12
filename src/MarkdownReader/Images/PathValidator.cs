using System;
using System.IO;
using System.Linq;

namespace MarkdownReader.Images;

public static class PathValidator
{
    /// <summary>
    /// Returns true iff the given path is non-null/non-empty, canonicalizes successfully,
    /// is not a UNC or DOS-device path, and falls under one of the whitelist roots.
    /// Fails closed: returns false on any malformed input rather than throwing.
    /// </summary>
    /// <remarks>
    /// Lexical canonicalization only — does NOT resolve symlinks/junctions. Callers
    /// should not include directories with attacker-controlled reparse points in the whitelist.
    /// </remarks>
    public static bool IsAllowed(string path, string[] whitelist)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;
        if (whitelist is null) return false;

        string full;
        try { full = Path.GetFullPath(path); }
        catch { return false; }

        // 拒绝 UNC (\\server\share) 和 DOS device paths (\\?\, \\.\) —
        // 后者会绕过 GetFullPath 的 .. 归一化，必须拒绝。
        if (full.StartsWith(@"\\", StringComparison.Ordinal)) return false;

        return whitelist.Any(root =>
        {
            if (string.IsNullOrWhiteSpace(root)) return false;
            try
            {
                var rootFull = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar)
                             + Path.DirectorySeparatorChar;
                return full.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase);
            }
            catch { return false; }
        });
    }
}
