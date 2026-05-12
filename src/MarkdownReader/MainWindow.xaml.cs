using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using MarkdownReader.Shell;

namespace MarkdownReader;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    public void OpenFile(string path)
    {
        string canonical;
        try { canonical = Path.GetFullPath(path); }
        catch { return; }

        // Switch to existing tab if same file already open
        foreach (TabItem t in Tabs.Items)
        {
            if (t.Tag is string existing && string.Equals(existing, canonical, StringComparison.OrdinalIgnoreCase))
            {
                Tabs.SelectedItem = t;
                return;
            }
        }

        // Placeholder content. Task 3.4 will replace this with a TabItemView containing WebView2.
        var content = new TextBlock
        {
            Text = $"[placeholder] {canonical}",
            Margin = new Thickness(20)
        };

        var tab = new TabItem
        {
            Header = Path.GetFileName(canonical),
            Content = content,
            Tag = canonical
        };
        Tabs.Items.Add(tab);
        Tabs.SelectedItem = tab;
    }

    public void BringToForeground() => ForegroundHelper.BringToFront(this);
}
