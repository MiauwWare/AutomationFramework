using System.Runtime.InteropServices;

namespace AutomationFramework.Processing;

/// <summary>Estimates the template-to-screen scale from the effective Windows DPI (96 DPI = 1.0).</summary>
public static class DpiScaleEstimator
{
    public static double EstimateInitialScale(double templateDpi = 96d)
    {
        if (templateDpi <= 0 || !double.IsFinite(templateDpi))
        {
            throw new ArgumentOutOfRangeException(nameof(templateDpi));
        }
        // A 96-DPI template on a 144-DPI desktop is expected to appear at roughly 1.5x scale.

        try { return Math.Clamp(GetDpiForSystem() / templateDpi, 0.25d, 4d); }
        catch (EntryPointNotFoundException) { return 1d; }
    }

    [DllImport("user32.dll")] private static extern uint GetDpiForSystem();
}
