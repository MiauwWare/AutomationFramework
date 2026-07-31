using OpenCvSharp;
using DrawingPoint = System.Drawing.Point;

namespace AutomationFramework.Matching;

internal static class CandidateVerifier
{
    public static (double Score, Point Location) BestScore(Mat image, Mat template, TemplateMatchModes mode)
    {
        // MatchTemplate returns a score map: one score for every legal template top-left coordinate.
        using var result = new Mat();
        Cv2.MatchTemplate(image, template, result, mode);
        Cv2.MinMaxLoc(result, out var min, out var max, out var minLocation, out var maxLocation);
        return IsLowerBetter(mode) ? (1d - min, minLocation) : (max, maxLocation);
    }

    public static double ScoreAt(Mat image, Mat template, DrawingPoint location, TemplateMatchModes mode)
    {
        // Restrict the score map to a single position so edge verification cannot drift to another feature.
        if (location.X < 0 || location.Y < 0 || location.X + template.Width > image.Width || location.Y + template.Height > image.Height)
        {
            return double.NegativeInfinity;
        }


        using var roi = new Mat(image, new Rect(location.X, location.Y, template.Width, template.Height));
        return BestScore(roi, template, mode).Score;
    }

    private static bool IsLowerBetter(TemplateMatchModes mode) => mode is TemplateMatchModes.SqDiff or TemplateMatchModes.SqDiffNormed;
}
