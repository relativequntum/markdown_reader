using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace MarkdownReader.Images;

public sealed class RemoteImageFetcher
{
    private readonly HttpClient _http;
    private readonly ImageCache _cache;
    private readonly ConcurrentDictionary<string, byte> _sessionBlacklist = new();

    private const string UA = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0.0.0 Safari/537.36 Edg/126.0.0.0";

    public RemoteImageFetcher(ImageCache cache)
    {
        _cache = cache;
        _http = new HttpClient(new HttpClientHandler
        {
            AutomaticDecompression = DecompressionMethods.All,
            AllowAutoRedirect = true
        })
        {
            Timeout = TimeSpan.FromSeconds(15)
        };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd(UA);
    }

    public async Task<(byte[], string)?> FetchAsync(string url)
    {
        if (_cache.TryGet(url, out var cached, out var meta)) return (cached, meta.ContentType);
        if (_sessionBlacklist.ContainsKey(url)) return null;

        var origin = RefererPolicy.OriginOf(url);
        var fetched = await TryFetch(url, origin);
        // Only retry without Referer if we sent one the first time.
        if (fetched is null && origin != null) fetched = await TryFetch(url, referer: null);
        if (fetched is null)
        {
            _sessionBlacklist.TryAdd(url, 0);
            return null;
        }

        _cache.Put(url, fetched.Value.Bytes, fetched.Value.ContentType);
        return fetched;
    }

    private async Task<(byte[] Bytes, string ContentType)?> TryFetch(string url, string? referer)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            if (referer != null) req.Headers.Referrer = new Uri(referer);
            using var resp = await _http.SendAsync(req);
            if (resp.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized) return null;
            if (!resp.IsSuccessStatusCode) return null;
            var bytes = await resp.Content.ReadAsByteArrayAsync();
            var ct = resp.Content.Headers.ContentType?.MediaType ?? "application/octet-stream";
            return (bytes, ct);
        }
        catch { return null; }
    }
}
