using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace MarkdownReader.Files;

public static class FileLoader
{
    public const long MaxBytes = 50L * 1024 * 1024;

    public static async Task<(string Text, long Bytes, Encoding Encoding)> LoadAsync(string path)
    {
        var size = new FileInfo(path).Length;
        if (size > MaxBytes) throw new IOException($"file too large: {size} bytes");

        await using var fs = new FileStream(path, FileMode.Open, FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete, 4096, FileOptions.SequentialScan | FileOptions.Asynchronous);
        var bytes = new byte[size];
        int off = 0;
        while (off < size)
        {
            var read = await fs.ReadAsync(bytes.AsMemory(off, (int)(size - off)));
            if (read == 0) break;
            off += read;
        }
        var (enc, text) = EncodingDetector.DetectAndDecode(bytes);
        return (text, size, enc);
    }
}
