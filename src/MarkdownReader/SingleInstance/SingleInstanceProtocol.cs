namespace MarkdownReader.SingleInstance;

public abstract record IpcMessage;
public sealed record OpenMessage(string Path) : IpcMessage;
public sealed record FocusMessage : IpcMessage;

public static class SingleInstanceProtocol
{
    public static string EncodeOpen(string path) => $"OPEN\t{path}\n";
    public static string EncodeFocus() => "FOCUS\n";

    public static IpcMessage? Decode(string raw)
    {
        var line = raw.TrimEnd('\r', '\n').Trim();
        if (line.Length == 0) return null;

        if (line == "FOCUS") return new FocusMessage();
        if (line.StartsWith("OPEN\t", System.StringComparison.Ordinal))
            return new OpenMessage(line["OPEN\t".Length..]);

        // bare-line fallback: 整行当路径
        return new OpenMessage(line);
    }
}
