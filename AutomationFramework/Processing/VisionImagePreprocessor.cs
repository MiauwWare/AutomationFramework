using OpenCvSharp;

namespace AutomationFramework.Processing;

/// <summary>Creates owned image representations used by the template matcher.</summary>
public static class VisionImagePreprocessor
{
    public static Mat ToGray(Mat source)
    {
        var output = new Mat();
        if (source.Channels() == 1)
        {
            source.CopyTo(output);
        }
        else
        {
            Cv2.CvtColor(source, output, ColorConversionCodes.BGR2GRAY);
        }


        return output;
    }

    public static Mat ToClahe(Mat source)
    {
        // CLAHE boosts local contrast without over-amplifying an entire bright/dark screenshot.
        using var gray = ToGray(source);
        var output = new Mat();
        using var clahe = Cv2.CreateCLAHE(4, new Size(8, 8));
        clahe.Apply(gray, output);
        return output;
    }

    public static Mat ToEdges(Mat source)
    {
        using var gray = ToGray(source);
        var output = new Mat();
        Cv2.Canny(gray, output, 80, 160);
        return output;
    }

    public static Mat BlendGrayAndClahe(Mat gray, Mat clahe, double tolerance)
    {
        // Tolerance smoothly moves from strict grayscale (0) toward contrast-normalized matching (1).
        var output = new Mat();
        Cv2.AddWeighted(gray, 1d - tolerance, clahe, tolerance, 0, output);
        return output;
    }

    public static IReadOnlyList<Mat> CreatePyramid(Mat source, int levels)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(levels);
        // Every level owns a clone, allowing callers to dispose the source immediately after construction.
        var pyramid = new List<Mat>(levels) { source.Clone() };
        for (var level = 1; level < levels; level++)
        {
            if (pyramid[^1].Width < 2 || pyramid[^1].Height < 2)
            {
                break;
            }


            var next = new Mat();
            Cv2.PyrDown(pyramid[^1], next);
            pyramid.Add(next);
        }
        return pyramid;
    }
}

/// <summary>An owned, single-capture set of screen representations.</summary>
internal sealed class VisionProcessedImage : IDisposable
{
    public required IReadOnlyList<Mat> GrayPyramid { get; init; }
    public required IReadOnlyList<Mat> ClahePyramid { get; init; }
    public required IReadOnlyList<Mat> EdgePyramid { get; init; }

    public static VisionProcessedImage Create(Mat screen, int levels)
    {
        // Capture preprocessing happens once per search; scale workers share these read-only Mats.
        using var gray = VisionImagePreprocessor.ToGray(screen);
        using var clahe = VisionImagePreprocessor.ToClahe(screen);
        using var edges = VisionImagePreprocessor.ToEdges(screen);
        return new VisionProcessedImage
        {
            GrayPyramid = VisionImagePreprocessor.CreatePyramid(gray, levels),
            ClahePyramid = VisionImagePreprocessor.CreatePyramid(clahe, levels),
            EdgePyramid = VisionImagePreprocessor.CreatePyramid(edges, levels)
        };
    }

    public void Dispose()
    {
        Dispose(GrayPyramid); 
        Dispose(ClahePyramid);
        Dispose(EdgePyramid);
    }

    public IReadOnlyList<Mat> CreateNormalizedPyramid(double tolerance)
    {
        // This is search-scoped rather than template-scoped, so the caller owns and disposes it.
        var pyramid = new List<Mat>(GrayPyramid.Count);
        for (var level = 0; level < GrayPyramid.Count; level++)
        {
            pyramid.Add(VisionImagePreprocessor.BlendGrayAndClahe(GrayPyramid[level], ClahePyramid[level], tolerance));
        }
        return pyramid;
    }

    private static void Dispose(IReadOnlyList<Mat> pyramid)
    {
        foreach (var mat in pyramid)
        {
            mat.Dispose();
        }

    }
}
