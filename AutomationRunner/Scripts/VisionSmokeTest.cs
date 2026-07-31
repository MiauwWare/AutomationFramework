using AutomationFramework;
using AutomationFramework.Extensions;
using AutomationFramework.Templates;
using AutomationFramework.VisionModels;
using AutomationRunner.Scripting;
using Microsoft.Extensions.Logging;
using System.Drawing;
using System.Drawing.Imaging;

namespace AutomationRunner.Scripts;

/// <summary>
/// Performs a safe live check of screen capture, template loading, DPI estimation, and matching.
/// On success it moves the mouse to the result center, but never clicks or sends keyboard input.
/// </summary>
public sealed class VisionSmokeTest : BaseScript
{
    // Change these constants when testing a different visible template.
    private const string TemplateFileName = VisionTemplateFileNames.MAIN_MENU_PASSWORD_TEXT;
    private const double MinimumConfidence = 0d;
    private const double ColorTolerance = 0d;

    private readonly IVisionTemplateResourceManager _templateManager;
    private readonly AutomationFramework.Cursor _cursor;
    private Vision? _vision;
    private VisionTemplateLease? _template;

    public VisionSmokeTest(
        ILogger<VisionSmokeTest> logger,
        IVisionTemplateResourceManager templateManager,
        AutomationFramework.Cursor cursor)
        : base(logger)
    {
        _templateManager = templateManager;
        _cursor = cursor;
    }

    public override string Name => "vision-smoke-test";
    public override string Description => "Verifies live capture and matching, then moves the cursor to a successful match.";

    protected override Task InitializeAsync(CancellationToken cancellationToken)
    {
        _vision = new Vision(_templateManager);
        _template = _vision.AcquireTemplate(TemplateFileName);
        return Task.CompletedTask;
    }

    protected override async Task RunAsync(CancellationToken cancellationToken)
    {
        var template = _template ?? throw new InvalidOperationException("Template is not initialized.");
        if (MinimumConfidence <= 0)
        {
            _logger.LogWarning("MinimumConfidence is {MinimumConfidence}. Any best-scoring location can be accepted; use at least 0.7 for a meaningful smoke test.", MinimumConfidence);
        }
        _logger.LogInformation(
            "Searching for {Template}. Metadata DPI: {TemplateDpi}; minimum confidence: {MinimumConfidence}",
            TemplateFileName,
            template.TemplateDpi?.ToString("0.##") ?? "not present (using VisionOptions fallback)",
            MinimumConfidence);

        var result = (_vision ?? throw new InvalidOperationException("Vision is not initialized.")).Find(
            template,
            new VisionSearchOptions { MinimumConfidence = MinimumConfidence, ColorTolerance = ColorTolerance },
            cancellationToken);

        if (result is null)
        {
            _logger.LogWarning("No match found for {Template}. Ensure the target UI is visible and lower the confidence only if appropriate.", TemplateFileName);
        }
        else
        {
            _logger.LogInformation(
                "Match found for {Template}: confidence {Confidence:F3}, local bounds {LocalBounds}, screen bounds {GlobalBounds}",
                TemplateFileName,
                result.Confidence,
                result.Bounds,
                result.GlobalBounds);

            var screenshotPath = SaveAnnotatedScreenshot(_vision!, result);
            _logger.LogInformation("Saved annotated match screenshot to {ScreenshotPath}", screenshotPath);

            var target = result.GlobalBounds.Center();
            await _cursor.MoveToAsync(target, cancellationToken: cancellationToken);
            _logger.LogInformation("Moved cursor to matched-template center {Target}", target);
        }
    }

    public override void Dispose()
    {
        _template?.Dispose();
        _vision?.Dispose();
    }

    private static string SaveAnnotatedScreenshot(Vision vision, VisionMatchResult result)
    {
        // The match bounds are local to SearchRegion, which is exactly the region captured here.
        using var screenshot = vision.Capture(result.SearchRegion);
        using var graphics = Graphics.FromImage(screenshot);
        using var outline = new Pen(Color.Lime, 3);
        graphics.DrawRectangle(outline, result.Bounds);

        var diagnosticsDirectory = Path.Combine(AppContext.BaseDirectory, "Diagnostics");
        Directory.CreateDirectory(diagnosticsDirectory);
        var outputPath = Path.Combine(diagnosticsDirectory, $"vision-smoke-test-{DateTime.Now:yyyyMMdd-HHmmss}.png");
        screenshot.Save(outputPath, ImageFormat.Png);
        return outputPath;
    }
}
