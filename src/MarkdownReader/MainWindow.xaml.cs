using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using MarkdownReader.Shell;
using MarkdownReader.Tabs;

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

        foreach (TabItem t in Tabs.Items)
        {
            if (t.Tag is string existing && string.Equals(existing, canonical, StringComparison.OrdinalIgnoreCase))
            {
                Tabs.SelectedItem = t;
                return;
            }
        }

        var view = new TabItemView();
        var tab = new TabItem
        {
            Header = Path.GetFileName(canonical),
            Content = view,
            Tag = canonical
        };
        view.HeaderTextChanged += text => tab.Header = text;
        view.RequestClose += () => Tabs.Items.Remove(tab);
        view.LoadFile(canonical);

        Tabs.Items.Add(tab);
        Tabs.SelectedItem = tab;
    }

    public void BringToForeground() => ForegroundHelper.BringToFront(this);
}
