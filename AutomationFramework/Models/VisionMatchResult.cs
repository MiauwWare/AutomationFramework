using System.Drawing;

namespace AutomationFramework.VisionModels;

public sealed record VisionMatchResult(Rectangle Bounds, double Confidence, Rectangle SearchRegion)
{
    public Rectangle GlobalBounds => new(SearchRegion.Left + Bounds.Left, SearchRegion.Top + Bounds.Top, Bounds.Width, Bounds.Height);
}
