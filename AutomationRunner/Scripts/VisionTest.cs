

using System.Data.Common;
using System.Numerics;
using AutomationFramework;
using AutomationFramework.Extensions;
using AutomationRunner.Scripting;
using AutomationRunner.Services;
using Microsoft.Extensions.Logging;
using OpenCvSharp;

namespace AutomationRunner.Scripts;

public sealed class VisionTest : BaseScript
{
    public VisionTest(
        AutomationFramework.Cursor cursor,
        IAutomationVisionFactory visionFactory,
        ILogger<VisionTest> logger)
        : base(logger)
    {
        _cursor = cursor;
        _visionFactory = visionFactory;
    }

    public override string Name => "vision-test";

    public override string Description => "A script to test vision functionality.";

    private readonly AutomationFramework.Cursor _cursor;
    private readonly IAutomationVisionFactory _visionFactory;
    AutomationFramework.Vision _vision = null!;

    private Dictionary<string, VisionTemplateLease> _templates = new Dictionary<string, VisionTemplateLease>();


    protected override Task InitializeAsync(CancellationToken cancellationToken)
    {
        _vision = _visionFactory.Create(TemplateMatchModes.CCoeffNormed);

        // Load templates
        AcquireTemplates
        (
            VisionTemplateFileNames.AB_TAILORING_BTN,
            VisionTemplateFileNames.TSM_MAX_BTN,
            VisionTemplateFileNames.TSM_CRAFT_BTN,
            VisionTemplateFileNames.TSM_CLOSE_BTN,
            VisionTemplateFileNames.AB_GILDED_TRADERS_BRUTOSAUR_BTN,
            VisionTemplateFileNames.AB_TARGET_MAIL_NPC_BTN,
            VisionTemplateFileNames.TSM_MAILBOX_GROUPS_BTN,
            VisionTemplateFileNames.TSM_MAIL_SELECTED_GROUPS_BTN
        );


        return Task.CompletedTask;
    }

    public override void Dispose()
    {   
        if (_vision is null)
        {
            return;
        }

        // Release templates
        foreach (var templateLease in _templates.Values)
        {
            templateLease.Dispose();
        }
        _templates.Clear();

        // Dispose vision
        _vision.Dispose();
    }

    protected override async Task RunAsync(CancellationToken cancellationToken)
    {
        var res = await _vision.FindImageAsync(
            _templates[VisionTemplateFileNames.TSM_MAIL_SELECTED_GROUPS_BTN].TemplateMat,
            searchRegion: Screen.PrimaryScreen?.Bounds,
            minConfidence: 0.6,
            cancellationToken: cancellationToken);

        if (res == null)
        {
            _logger.LogWarning("Image not found.");
            return;
        }

        var target = res.ToGlobalBounds().Center();
        _logger.LogInformation("Found match with confidence {Confidence} at {Bounds}", res.Confidence, res.ToGlobalBounds());
        await _cursor.MoveToAsync(target, cancellationToken: cancellationToken);

    }   

    private void AcquireTemplates(params string[] filenames)
    {
        ArgumentNullException.ThrowIfNull(filenames);

        foreach (var filename in filenames)
        {
            _templates[filename] = _vision.AcquireTemplateLease(filename);
        }
    }

    
}