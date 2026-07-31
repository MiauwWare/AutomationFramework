using OpenCvSharp;
using AutomationFramework.Processing;

namespace AutomationFramework.Templates;


public sealed class VisionTemplateResourceManager : IVisionTemplateResourceManager
{
    /// <summary>Identifies one reusable template derivative in the scaled-template cache.</summary>
    private readonly record struct ScaledTemplateCacheKey(
        int ScaleInThousandths,
        int ToleranceInThousandths,
        VisionTemplateRepresentation Representation)
    {
        public double Scale => ScaleInThousandths / 1000d;
        public double Tolerance => ToleranceInThousandths / 1000d;
    }

    internal sealed class ScaledTemplate : IDisposable
    {
        public required Mat Image { get; init; }
        public required IReadOnlyList<Mat> Pyramid { get; init; }
        
        // Level zero is also Image, so disposing the pyramid disposes every owned Mat exactly once.
        public void Dispose() 
        { 
            foreach (var item in Pyramid)
            {
                item.Dispose();
            }
        }
    }

    internal sealed class Entry : IDisposable
    {
        private readonly object _gate = new();
        
        // Quantized scale/tolerance avoids duplicate cache entries caused by floating-point stepping.
        private readonly Dictionary<ScaledTemplateCacheKey, ScaledTemplate> _scaled = new();
        
        public required Mat Original { get; init; }
        public required Mat Gray { get; init; }
        public required Mat Clahe { get; init; }
        public required Mat Edge { get; init; }
        public required IReadOnlyList<Mat> GrayPyramid { get; init; }
        public double? TemplateDpi { get; init; }
        public int ReferenceCount { get; set; } = 1;

        public ScaledTemplate GetScaled(double requestedScale, VisionTemplateRepresentation representation, double tolerance = 0)
        {
            if (requestedScale <= 0 || !double.IsFinite(requestedScale))
            {
                throw new ArgumentOutOfRangeException(nameof(requestedScale));
            }


            // Store decimals as thousandths so values such as 1.250 use one stable cache key (1250),
            // instead of slightly different floating-point values creating duplicate scaled Mats.
            var scaleInThousandths = Math.Max(1, (int)Math.Round(requestedScale * 1000));
            var toleranceInThousandths = Math.Clamp((int)Math.Round(tolerance * 1000), 0, 1000);
            var key = new ScaledTemplateCacheKey(
                ScaleInThousandths: scaleInThousandths,
                ToleranceInThousandths: toleranceInThousandths,
                Representation: representation);
            
            lock (_gate)
            {
                if (_scaled.TryGetValue(key, out var cached)) 
                {
                    return cached;
                }

                // Normalized templates are only materialized when requested; the base gray/CLAHE Mats remain cached.
                using var normalized = representation == VisionTemplateRepresentation.Normalized ? VisionImagePreprocessor.BlendGrayAndClahe(Gray, Clahe, key.Tolerance) : null;
                
                var source = representation switch { VisionTemplateRepresentation.Original => Original, VisionTemplateRepresentation.Clahe => Clahe, VisionTemplateRepresentation.Normalized => normalized!, VisionTemplateRepresentation.Edge => Edge, _ => Gray };
                var scale = key.Scale;
                var image = new Mat();
                Cv2.Resize(source, image, new Size(Math.Max(1, (int)Math.Round(source.Width * scale)), Math.Max(1, (int)Math.Round(source.Height * scale))), interpolation: scale < 1 ? InterpolationFlags.Area : InterpolationFlags.Linear);
                
                // Cache a pyramid with the scaled image so searches never resize or downsample this template again.
                var pyramid = VisionImagePreprocessor.CreatePyramid(image, 4);

                image.Dispose(); // Pyramid owns its level-zero clone.
                var result = new ScaledTemplate { Image = pyramid[0], Pyramid = pyramid };
                _scaled.Add(key, result);
                return result;
            }
        }

        public void Dispose()
        {
            lock (_gate)
            {
                // The last lease release frees every unmanaged Mat belonging to this cache entry.
                foreach (var item in _scaled.Values)
                {
                    item.Dispose();
                }

                foreach (var item in GrayPyramid)
                {
                    item.Dispose();
                }


                Original.Dispose(); 
                Gray.Dispose(); 
                Clahe.Dispose();
                 Edge.Dispose();
            }
        }
    }

    // The manager is singleton-scoped, but static storage also ensures all manager instances share template Mats.
    private static readonly Dictionary<string, Entry> Cache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<IntPtr, Entry> EntriesByMat = new();
    private static readonly object Gate = new();

    public VisionTemplateLease Acquire(string templateFilePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(templateFilePath);

        var path = Path.GetFullPath(templateFilePath);
        
        lock (Gate)
        {
            // Acquiring an existing asset adds a lease; it does not reread or preprocess the file.
            if (Cache.TryGetValue(path, out var cached))
            { 
                cached.ReferenceCount++;
                return new VisionTemplateLease(path, cached, Release);
            }

            var loaded = Load(path);
            var original = loaded.Image;
            try
            {
                using var gray = VisionImagePreprocessor.ToGray(original);
                var entry = new Entry
                {
                    Original = original,
                    Gray = gray.Clone(),
                    Clahe = VisionImagePreprocessor.ToClahe(original),
                    Edge = VisionImagePreprocessor.ToEdges(original),
                    GrayPyramid = VisionImagePreprocessor.CreatePyramid(gray, 4),
                    TemplateDpi = loaded.Dpi
                };

                Cache.Add(path, entry); 
                EntriesByMat.Add(original.CvPtr, entry);

                return new VisionTemplateLease(path, entry, Release);
            }
            catch 
            { 
                original.Dispose(); 
                throw; 
            }
        }
    }

    public Dictionary<string, VisionTemplateLease> Acquire(params string[] paths)
    {
        ArgumentNullException.ThrowIfNull(paths);

        var leases = new Dictionary<string, VisionTemplateLease>(StringComparer.OrdinalIgnoreCase);
        try 
        { 
            foreach (var path in paths)
            {
                leases.Add(path, Acquire(path));
            }

            return leases;
        }
        catch 
        { 
            foreach (var lease in leases.Values)
            {
                lease.Dispose();
            }

            throw;             
        }
    
    }

    internal static Entry? FindEntry(Mat template) 
    { 
        lock (Gate) 
        {
            return EntriesByMat.GetValueOrDefault(template.CvPtr); 
        }
    }

    private static void Release(string path)
    {
        lock (Gate)
        {
            if (!Cache.TryGetValue(path, out var entry))
            {
                throw new InvalidOperationException("Attempted to release a template that was not acquired: " + path);
            }

            // Only the final lease owns cleanup; earlier releases merely reduce the shared count.
            if (--entry.ReferenceCount != 0)
            {
                return;
            }


            Cache.Remove(path); EntriesByMat.Remove(entry.Original.CvPtr); entry.Dispose();
        }
    }

    private static LoadedTemplate Load(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("Template image was not found.", path);
        }


        var template = Cv2.ImRead(path, ImreadModes.Color);
        if (template.Empty()) { template.Dispose(); throw new InvalidOperationException("Template image is empty or could not be decoded."); }
        return new LoadedTemplate(template, TryReadDpi(path));
    }

    private static double? TryReadDpi(string path)
    {
        try
        {
            // OpenCV loads pixels only. GDI+ reads PNG pHYs or JPEG density metadata without keeping the file locked.
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var image = System.Drawing.Image.FromStream(stream, useEmbeddedColorManagement: false, validateImageData: false);
            var horizontal = image.HorizontalResolution;
            var vertical = image.VerticalResolution;

            if (horizontal <= 0 || vertical <= 0 || !float.IsFinite(horizontal) || !float.IsFinite(vertical)) return null;
            // A desktop UI has square pixels; unequal density values cannot produce one reliable match scale.
            return Math.Abs(horizontal - vertical) <= .01f ? horizontal : null;
        }
        catch (ArgumentException)
        {
            // Missing or unreadable metadata is normal; Vision falls back to its configured template DPI.
            return null;
        }
    }

    private sealed record LoadedTemplate(Mat Image, double? Dpi);
}
