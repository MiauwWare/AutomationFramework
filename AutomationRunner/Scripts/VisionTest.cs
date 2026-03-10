

using System.Data.Common;
using System.Numerics;
using AutomationFramework;
using AutomationFramework.Extensions;
using AutomationRunner.Scripting;
using Microsoft.Extensions.Configuration;
using OpenCvSharp;

namespace AutomationRunner.Scripts;

public sealed class VisionTest : BaseScript
{
    public override string Name => "vision-test";

    public override string Description => "A script to test vision functionality.";

    AutomationFramework.Cursor _cursor = new();
    AutomationFramework.Vision _vision = null!;

    Dictionary<string, Mat> _templates = new Dictionary<string, Mat>();


    protected override Task InitializeAsync(ScriptExecutionContext context, CancellationToken cancellationToken)
    {
        _vision = new AutomationFramework.Vision(new AutomationFramework.Vision.Options
        {
            OcrLanguage = "eng",
            OcrDataPath = context.Configuration.GetRequiredSection("OcrDataPath").Value!,
            TemplateMatchMode = TemplateMatchModes.CCoeffNormed
        });

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
        foreach (var templateFileName in _templates.Keys)
        {
            _vision.ReleaseTemplate(templateFileName);
        }

        // Dispose vision
        _vision.Dispose();
    }

    protected override async Task RunAsync(ScriptExecutionContext context, CancellationToken cancellationToken)
    {
        var res = await _vision.FindImageAsync(
            _templates[VisionTemplateFileNames.TSM_MAIL_SELECTED_GROUPS_BTN],
            searchRegion: Screen.PrimaryScreen?.Bounds,
            minConfidence: 0.6,
            cancellationToken: cancellationToken);

        if (res == null)
        {
            System.Console.WriteLine("Image not found.");
            return;
        }

        var target = res.ToGlobalBounds().Center();
        Console.WriteLine($"Found match with confidence {res.Confidence:F3} at {res.ToGlobalBounds()}");
        await _cursor.MoveToAsync(target, cancellationToken: cancellationToken);

    }   

    private void AcquireTemplates(params string[] filenames)
    {
        ArgumentNullException.ThrowIfNull(filenames);

        foreach (var filename in filenames)
        {
            _templates[filename] = _vision.AcquireTemplate(filename);
        }
    }

    
}