using System;
using System.Collections.Generic;
using System.IO;

namespace MarkdownReader.Files;

public interface IFileSystem
{
    bool FileExists(string path);
    byte[] ReadAllBytes(string path);
    void WriteAllBytes(string path, byte[] data);
    void Delete(string path);
    DateTime GetLastAccessTime(string path);
    void SetLastAccessTime(string path, DateTime t);
    IEnumerable<string> EnumerateFiles(string dir, string pattern);
    long GetSize(string path);
    void EnsureDir(string dir);
}

public sealed class RealFileSystem : IFileSystem
{
    public bool FileExists(string p) => File.Exists(p);
    public byte[] ReadAllBytes(string p) => File.ReadAllBytes(p);
    public void WriteAllBytes(string p, byte[] d)
    {
        EnsureDir(Path.GetDirectoryName(p)!);
        File.WriteAllBytes(p, d);
    }
    public void Delete(string p) { if (File.Exists(p)) File.Delete(p); }
    public DateTime GetLastAccessTime(string p) => File.GetLastAccessTimeUtc(p);
    public void SetLastAccessTime(string p, DateTime t) => File.SetLastAccessTimeUtc(p, t);
    public IEnumerable<string> EnumerateFiles(string d, string pat)
        => Directory.Exists(d) ? Directory.EnumerateFiles(d, pat, SearchOption.AllDirectories) : Array.Empty<string>();
    public long GetSize(string p) => new FileInfo(p).Length;
    public void EnsureDir(string d) { if (!Directory.Exists(d)) Directory.CreateDirectory(d); }
}
