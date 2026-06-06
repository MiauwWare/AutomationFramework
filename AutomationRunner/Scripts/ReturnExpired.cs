

using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using AutomationFramework;
using AutomationFramework.Extensions;
using AutomationRunner.Scripting;
using AutomationRunner.Services;
using Microsoft.Extensions.Logging;
using OpenCvSharp;

namespace AutomationRunner.Scripts;

public sealed class ReturnExpired : BaseScript
{
    public ReturnExpired(
        AutomationFramework.Cursor cursor,
        AutomationFramework.Keyboard keyboard,
        IAutomationVisionFactory visionFactory,
        ILogger<VisionShadesCrafting> logger)
        : base(logger)
    {
        _cursor = cursor;
        _keyboard = keyboard;
        _visionFactory = visionFactory;
    }

    public override string Name => "return-expired";

    public override string Description => "Return expired mail.";

    private readonly AutomationFramework.Cursor _cursor;
    private readonly AutomationFramework.Keyboard _keyboard;
    private readonly IAutomationVisionFactory _visionFactory;
    AutomationFramework.Vision _vision = null!;

    private Dictionary<string, VisionTemplateLease> _templateLeases = new Dictionary<string, VisionTemplateLease>();


    protected override Task InitializeAsync(CancellationToken cancellationToken)
    {
        _vision = _visionFactory.Create();

        // Load templates
        _templateLeases = _vision.AcquireTemplateLeases
        (
            VisionTemplateFileNames.TSM_MAILBOX_INBOX_BTN,
            VisionTemplateFileNames.TSM_MAILBOX_GROUPS_BTN,
            VisionTemplateFileNames.TSM_MAIL_SELECTED_GROUPS_BTN,
            VisionTemplateFileNames.TSM_OPEN_ALL_MAIL
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
        foreach (var templateLease in _templateLeases.Values)
        {
            templateLease.Dispose();
        }
        _templateLeases.Clear();

        // Dispose vision
        _vision.Dispose();
    }

    protected override async Task RunAsync(CancellationToken cancellationToken)
    {
        var searchRegion = Screen.PrimaryScreen?.Bounds;

        while (cancellationToken.IsCancellationRequested == false)
        {
            // open all mail
            if (await FindAndClickImageTemplateAsync(_templateLeases[VisionTemplateFileNames.TSM_OPEN_ALL_MAIL].TemplateMat, bounds => bounds.Scale(2, 1), cancellationToken: cancellationToken) == false)
            {
                _logger.LogWarning("Open all mail button not found.");
                break;
            }


            // wait for a bit
            await Task.Delay(TimeSpan.FromSeconds(20));

            // click on groups
            if (await FindAndClickImageTemplateAsync(_templateLeases[VisionTemplateFileNames.TSM_MAILBOX_GROUPS_BTN].TemplateMat, null, cancellationToken: cancellationToken) == false)
            {
                _logger.LogWarning("TSM_MAILBOX_GROUPS_BTN not found.");
                break;
            }


            // mail selected groups
            if (await FindAndClickImageTemplateAsync(_templateLeases[VisionTemplateFileNames.TSM_MAIL_SELECTED_GROUPS_BTN].TemplateMat, null, cancellationToken: cancellationToken) == false)
            {
                _logger.LogWarning("TSM_MAIL_SELECTED_GROUPS_BTN not found.");
                break;
            }


            await Task.Delay(TimeSpan.FromSeconds(12));

            // go back to inbox tab
            if (await FindAndClickImageTemplateAsync(_templateLeases[VisionTemplateFileNames.TSM_MAILBOX_INBOX_BTN].TemplateMat, null, cancellationToken: cancellationToken) == false)
            {
                _logger.LogWarning("TSM_MAIL_SELECTED_GROUPS_BTN not found.");
                break;
            }

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