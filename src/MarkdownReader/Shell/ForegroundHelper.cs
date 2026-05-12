using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace MarkdownReader.Shell;

internal static partial class ForegroundHelper
{
    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetForegroundWindow(IntPtr hWnd);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool AllowSetForegroundWindow(uint dwProcessId);

    private const uint ASFW_ANY = 0xFFFFFFFF;

    public static void BringToFront(Window w)
    {
        AllowSetForegroundWindow(ASFW_ANY);
        var hwnd = new WindowInteropHelper(w).Handle;
        if (w.WindowState == WindowState.Minimized) w.WindowState = WindowState.Normal;
        w.Show();
        w.Activate();
        SetForegroundWindow(hwnd);
    }
}
