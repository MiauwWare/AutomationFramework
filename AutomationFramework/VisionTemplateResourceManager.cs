using OpenCvSharp;

namespace AutomationFramework;

public interface IVisionTemplateResourceManager
{
    VisionTemplateLease Acquire(string templateFilePath);

    Dictionary<string, VisionTemplateLease> Acquire(params string[] templateFilePaths);
}

public sealed class VisionTemplateLease : IDisposable
{
    private readonly string _templateFilePath;
    private Action<string>? _release;

    internal VisionTemplateLease(string templateFilePath, Mat templateMat, Action<string> release)
    {
        _templateFilePath = templateFilePath;
        TemplateMat = templateMat;
        _release = release;
    }

    public Mat TemplateMat { get; }

    public void Dispose()
    {
        var release = Interlocked.Exchange(ref _release, null);
        if (release is null)
        {
            return;
        }

        release(_templateFilePath);
    }
}

public sealed class VisionTemplateResourceManager : IVisionTemplateResourceManager
{
    private sealed class Entry
    {
        public required Mat TemplateMat { get; init; }
        public int ReferenceCount { get; set; } = 1;
    }
    
    private static readonly Dictionary<string, Entry> _templateCache = new();

    private static readonly object _gate = new();

    /// <summary>
    /// Acquires a template image as an OpenCV Mat. 
    /// If the template has already been loaded, it will return a reference to the cached Mat and increment the reference count. 
    /// If the template has not been loaded, it will load it from disk, cache it, and return it. 
    /// The caller is responsible for calling Release() when they are done with the template to allow for proper resource cleanup.
    /// </summary>
    /// <param name="templateFilePath"></param>
    /// <returns></returns>
    public VisionTemplateLease Acquire(string templateFilePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(templateFilePath);

        lock (_gate)
        {

            if (_templateCache.TryGetValue(templateFilePath, out var cachedTemplate))
            {
                cachedTemplate.ReferenceCount++;
                return new VisionTemplateLease(templateFilePath, cachedTemplate.TemplateMat, ReleaseCore);
            }

            var loadedTemplate = LoadTemplate(templateFilePath);
            _templateCache[templateFilePath] = new Entry
            {
                TemplateMat = loadedTemplate
            };


            return new VisionTemplateLease(templateFilePath, loadedTemplate, ReleaseCore);
        }
    }

    public Dictionary<string, VisionTemplateLease> Acquire(params string[] templateFilePaths)
    {
        ArgumentNullException.ThrowIfNull(templateFilePaths);

        var leases = new Dictionary<string, VisionTemplateLease>(StringComparer.OrdinalIgnoreCase);

        foreach (var templateFilePath in templateFilePaths)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(templateFilePath);
            leases[templateFilePath] = Acquire(templateFilePath);
        }

        return leases;
    }


    /// <summary>
    /// Releases a previously acquired template. This should be called when the caller is done using the template to allow for proper resource cleanup.
    /// </summary>
    /// <param name="templateFilePath"></param>
    /// <exception cref="InvalidOperationException"></exception>
    private static void ReleaseCore(string templateFilePath)
    {
        lock (_gate)
        {
            if (_templateCache.TryGetValue(templateFilePath, out var cachedTemplate))
            {
                cachedTemplate.ReferenceCount--;

                if (cachedTemplate.ReferenceCount <= 0)
                {
                    cachedTemplate.TemplateMat.Dispose();
                    _templateCache.Remove(templateFilePath);
                }
            }
            else
            {
                throw new InvalidOperationException("Attempted to release a template that was not acquired: " + templateFilePath);
            }
        }
    }


    private static Mat LoadTemplate(string templateFilePath)
    {
        if (!File.Exists(templateFilePath))
        {
            throw new FileNotFoundException("Template image was not found.", templateFilePath);
        }

        return Cv2.ImRead(templateFilePath, ImreadModes.Color);
    }
}
