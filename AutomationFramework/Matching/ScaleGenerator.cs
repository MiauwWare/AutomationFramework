namespace AutomationFramework.Matching;

internal static class ScaleGenerator
{
    public static IReadOnlyList<double> Around(double estimate, double minimum, double maximum, double step)
    {
        // Try the DPI prediction first, then alternate below/above it to find likely UI scales early.
        var scales = new List<double>();
        for (var offset = 0d; estimate - offset >= minimum || estimate + offset <= maximum; offset += step)
        {
            if (estimate - offset >= minimum)
            {
                scales.Add(estimate - offset);
            }


            if (offset > 0 && estimate + offset <= maximum)
            {
                scales.Add(estimate + offset);
            }

        }
        return scales.DistinctBy(scale => Math.Round(scale, 3)).ToArray();
    }
}
