using System.Runtime.InteropServices;

namespace AutomationFramework.Windows;

internal static class WinSystemMetrics
{
    private const int SmXVirtualScreen = 76;
    private const int SmYVirtualScreen = 77;
    private const int SmCxVirtualScreen = 78;
    private const int SmCyVirtualScreen = 79;

    internal static (int Left, int Top, int Width, int Height) GetVirtualScreenBounds()
    {
        var left = GetSystemMetrics(SmXVirtualScreen);
        var top = GetSystemMetrics(SmYVirtualScreen);
        var width = Math.Max(1, GetSystemMetrics(SmCxVirtualScreen));
        var height = Math.Max(1, GetSystemMetrics(SmCyVirtualScreen));

        return (left, top, width, height);
    }

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);
}
