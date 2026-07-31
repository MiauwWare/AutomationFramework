using OpenCvSharp;

namespace AutomationFramework.VisionModels;

public sealed record VisionOptions
{
    /// <summary>Directory used to resolve relative template filenames.</summary>
    public string TemplateDirectory { get; init; } = Path.Combine(AppContext.BaseDirectory, "Assets", "Templates");
    public TemplateMatchModes TemplateMatchMode { get; init; } = TemplateMatchModes.CCoeffNormed;
    public bool EnableEdgeVerification { get; init; } = true;
    public int MaxDegreeOfParallelism { get; init; } = Environment.ProcessorCount;
    public int PyramidLevels { get; init; } = 4;
    public double TemplateDpi { get; init; } = 96d;
    /// <summary>Smallest allowed scaled-template dimension as a fraction of its original dimension.</summary>
    public double MinimumRelativeTemplateSize { get; init; } = .4d;

    internal void Validate()
    {
        if (string.IsNullOrWhiteSpace(TemplateDirectory)) throw new ArgumentException("Template directory is required.", nameof(TemplateDirectory));
        if (MaxDegreeOfParallelism <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(MaxDegreeOfParallelism));
        }


        if (PyramidLevels <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(PyramidLevels));
        }


        if (TemplateDpi <= 0 || !double.IsFinite(TemplateDpi))
        {
            throw new ArgumentOutOfRangeException(nameof(TemplateDpi));
        }

        if (MinimumRelativeTemplateSize is <= 0 or > 1 || !double.IsFinite(MinimumRelativeTemplateSize))
        {
            throw new ArgumentOutOfRangeException(nameof(MinimumRelativeTemplateSize));
        }

    }
}
