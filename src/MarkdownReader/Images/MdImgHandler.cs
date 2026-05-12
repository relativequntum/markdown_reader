using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Web.WebView2.Core;

namespace MarkdownReader.Images;

public sealed class MdImgHandler
{
    private readonly LocalImageResolver _local;
    private readonly Func<CoreWebView2Environment> _envProvider;

    // RemoteImageFetcher is wired in Task 3.7
    public Func<string, Task<(byte[], string)?>>? RemoteFetcher { get; set; }

    public MdImgHandler(LocalImageResolver local, Func<CoreWebView2Environment> envProvider)
    {
        _local = local;
        _envProvider = envProvider;
    }

    public void Register(CoreWebView2 wv)
    {
        wv.AddWebResourceRequestedFilter("mdimg://*", CoreWebView2WebResourceContext.All);
        wv.WebResourceRequested += async (_, e) =>
        {
            var deferral = e.GetDeferral();
            try
            {
                var url = MdImgUrlCodec.Decode(e.Request.Uri);
                (byte[], string)? result = url.Kind switch
                {
                    MdImgKind.Local => await _local.ResolveLocalAsync(url.BaseDir!, url.Payload),
                    MdImgKind.Abs => await _local.ResolveAbsAsync(url.Payload),
                    MdImgKind.Remote => RemoteFetcher != null ? await RemoteFetcher(url.Payload) : null,
                    _ => null
                };
                e.Response = result is (byte[] bytes, string ct)
                    ? MakeResponse(bytes, ct, 200, "OK")
                    : MakeResponse(PlaceholderSvg.Bytes(), PlaceholderSvg.ContentType, 404, "Not Found");
            }
            catch (FormatException)
            {
                e.Response = MakeResponse(PlaceholderSvg.Bytes(), PlaceholderSvg.ContentType, 400, "Bad Request");
            }
            catch
            {
                e.Response = MakeResponse(PlaceholderSvg.Bytes(), PlaceholderSvg.ContentType, 500, "Error");
            }
            finally { deferral.Complete(); }
        };
    }

    private CoreWebView2WebResourceResponse MakeResponse(byte[] bytes, string contentType, int status, string reason)
    {
        var ms = new MemoryStream(bytes);
        var env = _envProvider();
        var headers = $"Content-Type: {contentType}\r\nContent-Length: {bytes.Length}\r\nCache-Control: max-age=86400";
        return env.CreateWebResourceResponse(ms, status, reason, headers);
    }
}
