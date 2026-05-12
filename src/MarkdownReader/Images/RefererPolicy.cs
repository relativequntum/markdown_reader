using System;

namespace MarkdownReader.Images;

public static class RefererPolicy
{
    public static string? OriginOf(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return null;
        if (uri.Scheme is not ("http" or "https")) return null;
        var port = uri.IsDefaultPort ? "" : ":" + uri.Port;
        return $"{uri.Scheme}://{uri.Host}{port}/";
    }
}
