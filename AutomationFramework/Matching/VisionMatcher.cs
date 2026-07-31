using OpenCvSharp;
using AutomationFramework.Models;
using AutomationFramework.Processing;
using AutomationFramework.Templates;
using DrawingPoint = System.Drawing.Point;
using DrawingRectangle = System.Drawing.Rectangle;

namespace AutomationFramework.Matching;

/// <summary>Parallel scale search with pyramid refinement and location-consistent edge verification.</summary>
internal static class VisionMatcher
{
    // Tiny templates produce unstable correlation scores and are especially prone to random false positives.
    private const int MinimumTemplateWidth = 12;
    private const int MinimumTemplateHeight = 8;

    public static MatchCandidate? Find(
        VisionProcessedImage screen,
        VisionTemplateResourceManager.Entry? entry,
        Mat template,
        TemplateMatchModes mode,
        double minConfidence,
        float scaleStep,
        float minScale,
        float maxScale,
        double colorTolerance,
        bool useEdges,
        int maxDegreeOfParallelism,
        double initialScale,
        double minimumRelativeTemplateSize,
        CancellationToken cancellationToken)
    {
        Validate(scaleStep, minScale, maxScale);
        // A tolerance request uses a cached blend of the original grayscale and CLAHE representations.
        var representation = colorTolerance > 0 ? VisionTemplateRepresentation.Normalized : VisionTemplateRepresentation.Gray;
        var matchPyramid = colorTolerance > 0 ? screen.CreateNormalizedPyramid(colorTolerance) : screen.GrayPyramid;
        var scales = ScaleGenerator.Around(initialScale, minScale, maxScale, scaleStep);
        try
        {
            // Each scale is independent: its worker only reads cached Mats and creates its own temporary results.
            var candidates = ParallelScaleSearcher.Search
            (
                scales, 
                scale =>
                {
                    var grayTemplate = GetTemplate(entry, template, scale, representation, colorTolerance);
                    var edgeTemplate = useEdges ? GetTemplate(entry, template, scale, VisionTemplateRepresentation.Edge) : null;
                    try
                    {
                        var candidate = SearchPyramid(matchPyramid, screen.EdgePyramid, grayTemplate.Pyramid, edgeTemplate?.Pyramid,
                            template.Size(), minimumRelativeTemplateSize, scale, mode, useEdges);
                        return candidate;
                    }
                    finally
                    {
                        if (entry is null) { grayTemplate.Dispose(); edgeTemplate?.Dispose(); }
                    }
                }, 
                maxDegreeOfParallelism, 
                cancellationToken
            );

            // Pyramid refinement produces one best candidate per scale; select the highest verified result.
            var best = candidates.OrderByDescending(candidate => candidate.Confidence).FirstOrDefault();
            if (best is null || best.Confidence < minConfidence)
            {
                return null;
            }

            var final = GetTemplate(entry, template, best.Scale, representation, colorTolerance);
            try 
            { 
                return new MatchCandidate(best.Scale, best.Location, best.Confidence); 
            }
            finally 
            { 
                if (entry is null)
                {
                    final.Dispose();
                }
            }
        }
        finally
        {
            if (colorTolerance > 0)
            {
                foreach (var image in matchPyramid)
                {
                    image.Dispose();
                }
            }

        }
    }

    private static MatchCandidate? SearchPyramid(
        IReadOnlyList<Mat> grayPyramid,
        IReadOnlyList<Mat> edgePyramid,
        IReadOnlyList<Mat> templates,
        IReadOnlyList<Mat>? edges,
        Size originalTemplateSize,
        double minimumRelativeTemplateSize,
        double scale,
        TemplateMatchModes mode,
        bool useEdges)
    {
        // A very large original template may pass the absolute floor after excessive shrinking, so enforce both rules.
        if (!RetainsEnoughOriginalDetail(templates[0], originalTemplateSize, minimumRelativeTemplateSize)) return null;
        var lastLevel = Math.Min(grayPyramid.Count, templates.Count) - 1;
        // Start at the smallest level that still preserves enough template detail for a meaningful score.
        while (lastLevel >= 0 && !IsLargeEnough(templates[lastLevel]))
        {
            lastLevel--;
        }
        if (lastLevel < 0)
        {
            return null;
        }


        DrawingPoint location = default;
        double confidence = double.NegativeInfinity;

        for (var level = lastLevel; level >= 0; level--)
        {
            var image = grayPyramid[level];
            var template = templates[level];
            if (template.Width > image.Width || template.Height > image.Height)
            {
                return null;
            }

            // Search globally at the smallest image, then only inspect a small projected window at each finer level.

            var roi = level == lastLevel
                ? new DrawingRectangle(0, 0, image.Width, image.Height)
                : SearchWindow(location, image.Size(), template.Size());
            if (roi.Width < template.Width || roi.Height < template.Height)
            {
                return null;
            }


            using var local = new Mat(image, new Rect(roi.X, roi.Y, roi.Width, roi.Height));
            var (grayScore, matchLocation) = CandidateVerifier.BestScore(local, template, mode);
            var absolute = new DrawingPoint(roi.X + matchLocation.X, roi.Y + matchLocation.Y);
            confidence = grayScore;

            if (useEdges && edges is not null && level < edgePyramid.Count && level < edges.Count)
            {
                // Verify at the grayscale location. Searching the edge image independently would permit two unrelated matches.
                var edgeScore = CandidateVerifier.ScoreAt(edgePyramid[level], edges[level], absolute, mode);
                if (double.IsFinite(edgeScore))
                {
                    confidence = (grayScore * .8d) + (edgeScore * .2d);
                }

            }
            // PyrDown halves each dimension, so project this top-left position into the next finer level.
            location = level == 0 ? absolute : new DrawingPoint(absolute.X * 2, absolute.Y * 2);
        }
        return new MatchCandidate(scale, location, confidence);
    }

    private static VisionTemplateResourceManager.ScaledTemplate GetTemplate(VisionTemplateResourceManager.Entry? entry, Mat template, double scale, VisionTemplateRepresentation representation, double tolerance = 0)
    {
        // Leased templates reuse their preprocessed/scaled cache. Raw Mats use equivalent temporary data.
        if (entry is not null)
        {
            return entry.GetScaled(scale, representation, tolerance);
        }


        using var source = representation switch
        {
            VisionTemplateRepresentation.Clahe => VisionImagePreprocessor.ToClahe(template),
            VisionTemplateRepresentation.Normalized => CreateNormalized(template, tolerance),
            VisionTemplateRepresentation.Edge => VisionImagePreprocessor.ToEdges(template),
            _ => VisionImagePreprocessor.ToGray(template)
        };
        var scaled = new Mat();
        Cv2.Resize(source, scaled, new Size(Math.Max(1, (int)Math.Round(source.Width * scale)), Math.Max(1, (int)Math.Round(source.Height * scale))), interpolation: scale < 1 ? InterpolationFlags.Area : InterpolationFlags.Linear);
        var pyramid = VisionImagePreprocessor.CreatePyramid(scaled, 4);
        scaled.Dispose();
        return new VisionTemplateResourceManager.ScaledTemplate { Image = pyramid[0], Pyramid = pyramid };
    }

    private static Mat CreateNormalized(Mat template, double tolerance)
    {
        using var gray = VisionImagePreprocessor.ToGray(template);
        using var clahe = VisionImagePreprocessor.ToClahe(template);
        return VisionImagePreprocessor.BlendGrayAndClahe(gray, clahe, tolerance);
    }

    private static DrawingRectangle SearchWindow(DrawingPoint predicted, Size imageSize, Size templateSize)
    {
        var margin = Math.Max(8, Math.Max(templateSize.Width, templateSize.Height) / 3);
        var rect = new DrawingRectangle(predicted.X - margin, predicted.Y - margin, templateSize.Width + margin * 2, templateSize.Height + margin * 2);
        var left = Math.Clamp(rect.Left, 0, imageSize.Width); var top = Math.Clamp(rect.Top, 0, imageSize.Height);
        var right = Math.Clamp(rect.Right, 0, imageSize.Width); var bottom = Math.Clamp(rect.Bottom, 0, imageSize.Height);
        return DrawingRectangle.FromLTRB(left, top, right, bottom);
    }

    private static void Validate(float step, float min, float max)
    {
        if (step <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(step));
        }


        if (min <= 0 || max < min)
        {
            throw new ArgumentOutOfRangeException(nameof(min));
        }

    }

    private static bool IsLargeEnough(Mat template) => template.Width >= MinimumTemplateWidth && template.Height >= MinimumTemplateHeight;

    private static bool RetainsEnoughOriginalDetail(Mat scaledTemplate, Size originalTemplate, double minimumRelativeSize) =>
        scaledTemplate.Width >= originalTemplate.Width * minimumRelativeSize &&
        scaledTemplate.Height >= originalTemplate.Height * minimumRelativeSize;

}
