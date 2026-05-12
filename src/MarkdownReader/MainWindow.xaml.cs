namespace MarkdownReader;

public partial class MainWindow : System.Windows.Window
{
    public MainWindow() { InitializeComponent(); }

    public void OpenFile(string path)
    {
        // Task 3.3+ will implement tab creation/switching.
        // For now this is a stub so Program.cs compiles.
        _ = path;
    }

    public void BringToForeground()
    {
        // Task 3.3 will use ForegroundHelper. For now: Activate().
        if (WindowState == System.Windows.WindowState.Minimized)
            WindowState = System.Windows.WindowState.Normal;
        Activate();
    }
}
