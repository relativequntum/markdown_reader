using System;
using System.Text;
using System.Web;

namespace MarkdownReader.Images;

public enum MdImgKind { Local, Abs, Remote }

public sealed record MdImgUrl(MdImgKind Kind, string Payload, string? BaseDir);

public static class MdImgUrlCodec
{
    private const string Scheme = "mdimg://";

    public static string EncodeLocal(string relPath, string baseDir)
        => $"{Scheme}local/{B64UEncode(relPath)}?base={B64UEncode(baseDir)}";

    public static string EncodeAbs(string absPath)
        => $"{Scheme}abs/{B64UEncode(absPath)}";

    public static string EncodeRemote(string url)
        => $"{Scheme}remote/{B64UEncode(url)}";

    public static MdImgUrl Decode(string url)
    {
        if (string.IsNullOrWhiteSpace(url) || !url.StartsWith(Scheme, StringComparison.Ordinal))
            throw new FormatException($"not an mdimg URL: {url}");

        var rest = url[Scheme.Length..];          // local/<b64>?base=<b64>
        var slash = rest.IndexOf('/');
        if (slash < 0) throw new FormatException("missing kind");

        var kindStr = rest[..slash];
        var tail = rest[(slash + 1)..];
        string? baseDir = null;
        var payloadB64 = tail;
        var qIdx = tail.IndexOf('?');
        if (qIdx >= 0)
        {
            payloadB64 = tail[..qIdx];
            var query = HttpUtility.ParseQueryString(tail[(qIdx + 1)..]);
            var b = query["base"];
            if (b != null) baseDir = B64UDecode(b);
        }

        var payload = B64UDecode(payloadB64);
        var kind = kindStr switch
        {
            "local" => MdImgKind.Local,
            "abs" => MdImgKind.Abs,
            "remote" => MdImgKind.Remote,
            _ => throw new FormatException($"unknown kind {kindStr}")
        };
        return new MdImgUrl(kind, payload, baseDir);
    }

    private static string B64UEncode(string s)
    {
        var bytes = Encoding.UTF8.GetBytes(s);
        return Convert.ToBase64String(bytes)
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');
    }

    private static string B64UDecode(string s)
    {
        try
        {
            var pad = (4 - s.Length % 4) % 4;
            var b64 = s.Replace('-', '+').Replace('_', '/') + new string('=', pad);
            return Encoding.UTF8.GetString(Convert.FromBase64String(b64));
        }
        catch (FormatException) { throw; }
        catch (Exception ex) { throw new FormatException("invalid base64url", ex); }
    }
}
