using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using MarkdownReader.Settings;
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

    private void OnThemeSystem(object sender, RoutedEventArgs e) => SetTheme(ThemeChoice.System);
    private void OnThemeLight (object sender, RoutedEventArgs e) => SetTheme(ThemeChoice.Light);
    private void OnThemeDark  (object sender, RoutedEventArgs e) => SetTheme(ThemeChoice.Dark);

    private void SetTheme(ThemeChoice t)
    {
        var app = (App)Application.Current;
        app.Settings.Theme = t;
        try { SettingsStore.Save(AppPaths.SettingsFile, app.Settings); } catch { /* best effort */ }
        foreach (TabItem ti in Tabs.Items)
            if (ti.Content is Tabs.TabItemView v) v.PushTheme(t);
    }

    private void OnClearCache(object sender, RoutedEventArgs e)
    {
        try
        {
            if (Directory.Exists(AppPaths.CacheDir)) Directory.Delete(AppPaths.CacheDir, true);
            Directory.CreateDirectory(AppPaths.CacheDir);
            MessageBox.Show("图片缓存已清理。", "完成");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"清理失败：{ex.Message}", "错误");
        }
    }

    private void OnOpenCacheDir(object sender, RoutedEventArgs e)
    {
        try
        {
            Directory.CreateDirectory(AppPaths.CacheDir);
            Process.Start(new ProcessStartInfo(AppPaths.CacheDir) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"打开失败：{ex.Message}", "错误");
        }
    }

    private void OnRegisterAssoc(object sender, RoutedEventArgs e)
    {
        try
        {
            FileAssociation.Register();
            MessageBox.Show("已注册。重启资源管理器或重新打开窗口后，双击 .md 即可。", "完成");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"注册失败：{ex.Message}", "错误");
        }
    }

    private void OnUnregisterAssoc(object sender, RoutedEventArgs e)
    {
        FileAssociation.Unregister();
        MessageBox.Show("已取消文件关联。", "完成");
    }
}
