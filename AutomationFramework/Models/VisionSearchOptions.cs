using System.Drawing;

namespace AutomationFramework.VisionModels;

public sealed record VisionSearchOptions
{
    public Rectangle? SearchRegion { get; init; }
    public double MinimumConfidence { get; init; } = .8;
    public float MinimumScale { get; init; } = .25f;
    public float MaximumScale { get; init; } = 2f;
    public float ScaleStep { get; init; } = .05f;
    public double ColorTolerance { get; init; }

    internal void Validate()
    {
        if (MinimumConfidence is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(MinimumConfidence));
        }


        if (ColorTolerance is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(ColorTolerance));
        }


        if (MinimumScale <= 0 || MaximumScale < MinimumScale || ScaleStep <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(MinimumScale));
        }


        if (SearchRegion is { Width: <= 0 } or { Height: <= 0 })
        {
            throw new ArgumentOutOfRangeException(nameof(SearchRegion));
        }

    }
}
