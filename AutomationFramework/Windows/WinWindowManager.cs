using System.Runtime.InteropServices;

namespace AutomationFramework.Windows;

public static class WinWindowManager
{
    public static IntPtr GetForegroundWindowHandle() => GetForegroundWindow();

    public static IntPtr FindWindow(string? className, string? windowName)
        => FindWindowNative(className, windowName);

    public static bool TryFindWindow(string? className, string? windowName, out IntPtr handle)
    {
        handle = FindWindow(className, windowName);
        return handle != IntPtr.Zero;
    }

    public static bool ShowWindow(IntPtr handle, WindowShowCommand command)
    {
        if (handle == IntPtr.Zero)
        {
            return false;
        }

        return ShowWindowNative(handle, (int)command);
    }

    public static bool IsMinimized(IntPtr handle)
    {
        if (handle == IntPtr.Zero)
        {
            return false;
        }

        return IsIconic(handle);
    }

    public static bool FocusWindow(IntPtr handle)
    {
        if (handle == IntPtr.Zero)
        {
            return false;
        }

        return SetForegroundWindow(handle);
    }

    public static bool RestoreAndFocusWindow(IntPtr handle)
    {
        if (handle == IntPtr.Zero)
        {
            return false;
        }

        if (IsIconic(handle))
        {
            ShowWindowNative(handle, (int)WindowShowCommand.Restore);
        }

        return SetForegroundWindow(handle);
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "FindWindowW")]
    private static extern IntPtr FindWindowNative(string? lpClassName, string? lpWindowName);

    [DllImport("user32.dll", EntryPoint = "ShowWindow")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindowNative(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsIconic(IntPtr hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr hWnd);
}

public enum WindowShowCommand
{
    Hide = 0,
    ShowNormal = 1,
    ShowMinimized = 2,
    ShowMaximized = 3,
    ShowNoActivate = 4,
    Show = 5,
    Minimize = 6,
    ShowMinNoActive = 7,
    ShowNA = 8,
    Restore = 9,
    ShowDefault = 10,
    ForceMinimize = 11
}
