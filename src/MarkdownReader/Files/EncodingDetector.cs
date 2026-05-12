using System;
using System.Text;

namespace MarkdownReader.Files;

public static class EncodingDetector
{
    static EncodingDetector() => Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

    public static (Encoding Encoding, string Text) DetectAndDecode(byte[] bytes)
    {
        if (bytes.Length >= 3 && bytes[0]==0xEF && bytes[1]==0xBB && bytes[2]==0xBF)
            return (new UTF8Encoding(false), Encoding.UTF8.GetString(bytes, 3, bytes.Length-3));
        if (bytes.Length >= 2 && bytes[0]==0xFF && bytes[1]==0xFE)
            return (Encoding.Unicode, Encoding.Unicode.GetString(bytes, 2, bytes.Length-2));
        if (bytes.Length >= 2 && bytes[0]==0xFE && bytes[1]==0xFF)
            return (Encoding.BigEndianUnicode, Encoding.BigEndianUnicode.GetString(bytes, 2, bytes.Length-2));

        // 严格 UTF-8 试探
        var strict = new UTF8Encoding(false, throwOnInvalidBytes: true);
        try
        {
            var text = strict.GetString(bytes);
            return (strict, text);
        }
        catch (DecoderFallbackException)
        {
            // 退到系统 ANSI（中文 Windows 通常是 GBK/936）
            var ansi = Encoding.GetEncoding(0);
            return (ansi, ansi.GetString(bytes));
        }
    }
}
