namespace MarkdownReader.Images;

public static class PlaceholderSvg
{
    public const string ContentType = "image/svg+xml";

    public static byte[] Bytes(string label = "⚠ 图片加载失败")
    {
        var svg = $@"<svg xmlns='http://www.w3.org/2000/svg' width='300' height='80' viewBox='0 0 300 80'>
<rect width='300' height='80' fill='#f5f5f5' stroke='#bbb' stroke-dasharray='4 4'/>
<text x='150' y='45' text-anchor='middle' font-family='sans-serif' font-size='14' fill='#777'>{System.Net.WebUtility.HtmlEncode(label)}</text>
</svg>";
        return System.Text.Encoding.UTF8.GetBytes(svg);
    }
}
