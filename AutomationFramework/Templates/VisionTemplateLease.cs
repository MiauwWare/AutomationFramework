using OpenCvSharp;

namespace AutomationFramework.Templates;


/// <summary>A reference-counted template and every cached representation derived from it.</summary>
public sealed class VisionTemplateLease : IDisposable
{
    private readonly string _path;
    private Action<string>? _release;
    internal VisionTemplateResourceManager.Entry Entry { get; }

    internal VisionTemplateLease(string path, VisionTemplateResourceManager.Entry entry, Action<string> release)
        => (_path, Entry, _release) = (path, entry, release);

    // These Mats are cache-owned and read-only from the caller's perspective; never dispose or mutate them.
    public Mat TemplateMat => Entry.Original;
    /// <summary>DPI read from PNG/JPEG metadata; null when no valid density metadata is present.</summary>
    public double? TemplateDpi => Entry.TemplateDpi;
    public Mat GrayTemplate => Entry.Gray;
    public Mat EdgeTemplate => Entry.Edge;
    public IReadOnlyList<Mat> Pyramid => Entry.GrayPyramid;

    public Mat GetScaledTemplate(double scale, VisionTemplateRepresentation representation = VisionTemplateRepresentation.Gray)
    {
        // The returned Mat remains valid for the life of this lease and is reused by later requests for this scale.
        ThrowIfReleased();
        return Entry.GetScaled(scale, representation).Image;
    }

    // Atomically takes the release callback so only the first Dispose decrements the shared template reference count.
    // Later calls see null and are harmless.
    public void Dispose()
    {
        Interlocked.Exchange(ref _release, null)?.Invoke(_path);
    }
    private void ThrowIfReleased() 
    { 
        if (_release is null)
        {
            throw new ObjectDisposedException(nameof(VisionTemplateLease));
        }
    }
}
