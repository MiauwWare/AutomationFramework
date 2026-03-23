using System;
using AutomationFramework;
using AutomationFramework.Extensions;
using AutomationRunner.Scripting;
using AutomationRunner.Services;
using Microsoft.Extensions.Logging;
using OpenCvSharp;

namespace AutomationRunner.Scripts;

public class VisionDisenchantingTest : BaseScript
{
    public VisionDisenchantingTest(
        AutomationFramework.Cursor cursor,
        AutomationFramework.Keyboard keyboard,
        IAutomationVisionFactory visionFactory,
        ILogger<VisionDisenchantingTest> logger)
        : base(logger)
    {
        _cursor = cursor;
        _keyboard = keyboard;
        _visionFactory = visionFactory;
    }

    public override string Name => "vision-disenchanting-test";

    public override string Description => "Disenchants and opens mailbox";

    private readonly AutomationFramework.Cursor _cursor;
    private AutomationFramework.Vision _vision = null!;
    private readonly AutomationFramework.Keyboard _keyboard;
    private readonly IAutomationVisionFactory _visionFactory;

    private Dictionary<string, VisionTemplateLease> _templates = new Dictionary<string, VisionTemplateLease>();

    protected override Task InitializeAsync(CancellationToken cancellationToken)
    {
        _vision = _visionFactory.Create();

        AcquireTemplates
        (
            VisionTemplateFileNames.TSM_OPEN_ALL_MAIL,
            VisionTemplateFileNames.AB_TSM_DESTROY_BTN,
            VisionTemplateFileNames.TSM_DESTROY_NEXT_BTN,
            VisionTemplateFileNames.TSM_CLOSE_BTN
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
        while (cancellationToken.IsCancellationRequested == false)
        {
            //wait for disenchanting to free up bag space
            await Task.Delay(TimeSpan.FromSeconds(5).ApplyRandomFactor());

            // Open mailbox
            await _keyboard.PressKeyAsync(AutomationFramework.VirtualKey.F, cancellationToken: cancellationToken);
            await Task.Delay(TimeSpan.FromMilliseconds(500).ApplyRandomFactor(), cancellationToken);

        
            if (await FindAndClickImageTemplateAsync(_templates[VisionTemplateFileNames.TSM_OPEN_ALL_MAIL].TemplateMat, bounds => bounds.Padd(40, 2), cancellationToken: cancellationToken) == false)
            {
                _logger.LogWarning("Open all mail button not found.");
                break;
            }
    
            // wait for all mail to open
            await Task.Delay(TimeSpan.FromSeconds(1).ApplyRandomFactor(), cancellationToken);


            //close mailbox by pressing the close button
            if (await FindAndClickImageTemplateAsync(_templates[VisionTemplateFileNames.TSM_CLOSE_BTN].TemplateMat, (bounds) => bounds.Scale(0.5f), cancellationToken: cancellationToken) == false)
            {
                _logger.LogWarning("TSM close button not found.");
                break;
            }
        }
    }

    private void AcquireTemplates(params string[] filenames)
    {
        ArgumentNullException.ThrowIfNull(filenames);

        foreach (var filename in filenames)
        {
            _templates[filename] = _vision.AcquireTemplateLease(filename);
        }
    }


    private async Task<bool> FindAndClickImageTemplateAsync(Mat template, Func<Rectangle, Rectangle>? boundsManipulations = null, float confidence = 0.7f, CancellationToken cancellationToken = default)
    {
        // find the template on screen with retries, if not found, return false
        var imageMatch = await Task.RunWithRetry
        (
            (cancellationToken) => _vision.FindImageAsync
            (
                template,
                confidence,
                searchRegion: Screen.PrimaryScreen?.Bounds,
                cancellationToken: cancellationToken
            ),
            successCondition: (result) => result != null,
            maxRetries: 3,
            retryDelay: TimeSpan.FromSeconds(1),
            cancellationToken
        );

        if (imageMatch == null)
        {
            return false;
        }

        // Move cursor to a random point within the target bounds and click
        var targetPos = (boundsManipulations != null ? boundsManipulations(imageMatch.ToGlobalBounds()) : imageMatch.ToGlobalBounds()).GetRandomPointInBounds();
        await _cursor.MoveToAsync(targetPos, cancellationToken: cancellationToken);
        await Task.Delay(TimeSpan.FromMilliseconds(250).ApplyRandomFactor(), cancellationToken);
        await _cursor.ClickAsync(cancellationToken: cancellationToken);

        return true;
    }
}
