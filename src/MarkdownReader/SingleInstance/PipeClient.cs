namespace MarkdownReader.SingleInstance;

/// <summary>
/// Placeholder. Real implementation lands in Task 3.2.
/// Returns false (caller will treat as "main instance dead" and escalate).
/// </summary>
public static class PipeClient
{
    public static bool Send(string pipeName, string message, int timeoutMs) => false;
}
