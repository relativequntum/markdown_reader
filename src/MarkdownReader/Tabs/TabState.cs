using System;

namespace MarkdownReader.Tabs;

public sealed class TabState
{
    public string FilePath = "";
    public string BaseDir = "";
    public string? RawText;
    public DateTime LoadedAt;
    public bool IsDeleted;
}
