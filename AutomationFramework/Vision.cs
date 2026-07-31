using System.Drawing;
using AutomationFramework.Matching;
using AutomationFramework.Processing;
using AutomationFramework.Templates;
using AutomationFramework.VisionModels;
using OpenCvSharp;

namespace AutomationFramework;

/// <summary>Small façade for screen capture and scale-aware template matching.</summary>
public sealed class Vision : IDisposable
{
    private readonly IVisionTemplateResourceManager _templates;
    private readonly VisionOptions _options;
    private bool _disposed;

    public Vision(IVisionTemplateResourceManager templates, VisionOptions? options = null)
    {
        _templates = templates ?? throw new ArgumentNullException(nameof(templates));
        _options = options ?? new VisionOptions();
        _options.Validate();
    }

    public VisionTemplateLease AcquireTemplate(string templateFilePath)
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrWhiteSpace(templateFilePath);
        return _templates.Acquire(ResolveTemplatePath(templateFilePath));
    }

    /// <summary>
    /// Acquires several templates as independent leases. If one acquisition fails, any earlier leases are released.
    /// </summary>
    public Dictionary<string, VisionTemplateLease> AcquireTemplates(params string[] templateFilePaths)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(templateFilePaths);
        foreach (var path in templateFilePaths) ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (templateFilePaths.Distinct(StringComparer.OrdinalIgnoreCase).Count() != templateFilePaths.Length)
            throw new ArgumentException("Each template path must be unique.", nameof(templateFilePaths));

        var resolvedPaths = templateFilePaths.Select(ResolveTemplatePath).ToArray();
        var leasesByResolvedPath = _templates.Acquire(resolvedPaths);
        // Keep the caller-facing keys (normally filenames) while the resource manager caches by full path.
        return templateFilePaths.ToDictionary(path => path, path => leasesByResolvedPath[ResolveTemplatePath(path)], StringComparer.OrdinalIgnoreCase);
    }

    public Bitmap Capture(Rectangle? region = null)
    {
        ThrowIfDisposed();
        var area = region ?? VirtualScreen();
        if (area.Width <= 0 || area.Height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(region));
        }


        var bitmap = new Bitmap(area.Width, area.Height);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.CopyFromScreen(area.Location, System.Drawing.Point.Empty, area.Size);
        return bitmap;
    }

    public VisionMatchResult? Find(VisionTemplateLease template, VisionSearchOptions? search = null, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(template);
        var request = search ?? new VisionSearchOptions();
        request.Validate();
        var region = request.SearchRegion ?? VirtualScreen();
        using var screenshot = Capture(region);
        using var screen = Decode(screenshot);
        using var processed = VisionProcessedImage.Create(screen, _options.PyramidLevels);
        // Vision captures and preprocesses once; the matcher owns the scale/pyramid search strategy.
        var result = VisionMatcher.Find(processed, template.Entry, template.TemplateMat, _options.TemplateMatchMode,
            request.MinimumConfidence, request.ScaleStep, request.MinimumScale, request.MaximumScale, request.ColorTolerance,
            _options.EnableEdgeVerification, _options.MaxDegreeOfParallelism,
            DpiScaleEstimator.EstimateInitialScale(template.TemplateDpi ?? _options.TemplateDpi),
            _options.MinimumRelativeTemplateSize, cancellationToken);
        if (result is null)
        {
            return null;
        }
        // The cached scaled template is the authoritative source for the returned match dimensions.

        var scaled = template.GetScaledTemplate(result.Scale, VisionTemplateRepresentation.Gray);
        return new VisionMatchResult(new Rectangle(result.Location.X, result.Location.Y, scaled.Width, scaled.Height), result.Confidence, region);
    }

    private static Mat Decode(Bitmap bitmap)
    {
        using var stream = new MemoryStream();
        bitmap.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
        return Cv2.ImDecode(stream.ToArray(), ImreadModes.Color);
    }

    private static Rectangle VirtualScreen()
    {
        var (left, top, width, height) = Windows.WinSystemMetrics.GetVirtualScreenBounds();
        return new Rectangle(left, top, width, height);
    }

    private string ResolveTemplatePath(string templateFilePath) => Path.GetFullPath(Path.IsPathRooted(templateFilePath)
        ? templateFilePath
        : Path.Combine(_options.TemplateDirectory, templateFilePath));

    private void ThrowIfDisposed() { if (_disposed)
        {
            throw new ObjectDisposedException(nameof(Vision));
        }
    }
    public void Dispose() => _disposed = true;
}
