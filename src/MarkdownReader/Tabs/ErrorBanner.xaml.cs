using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace MarkdownReader.Tabs;

public partial class ErrorBanner : UserControl
{
    public ErrorBanner()
    {
        InitializeComponent();
        Visibility = Visibility.Collapsed;
    }

    public void Show(string kind, string message, params (string Label, Action OnClick)[] actions)
    {
        Msg.Text = message;
        Root.Background = kind == "error"
            ? new SolidColorBrush(Color.FromRgb(0xFF, 0xD6, 0xD6))
            : new SolidColorBrush(Color.FromRgb(0xFF, 0xE9, 0xB3));
        Actions.Children.Clear();
        foreach (var (lbl, fn) in actions)
        {
            var b = new Button { Content = lbl, Margin = new Thickness(6, 0, 0, 0), Padding = new Thickness(8, 2, 8, 2) };
            b.Click += (_, _) => fn();
            Actions.Children.Add(b);
        }
        Visibility = Visibility.Visible;
    }

    public void Hide() => Visibility = Visibility.Collapsed;
}
