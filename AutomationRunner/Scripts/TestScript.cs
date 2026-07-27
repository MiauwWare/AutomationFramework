

using System.Numerics;
using AutomationFramework;
using AutomationFramework.Extensions;
using AutomationRunner.Scripting;
using AutomationRunner.Services;
using Microsoft.Extensions.Logging;
using OpenCvSharp;

namespace AutomationRunner.Scripts;

public sealed class TestScript : BaseScript
{
    public TestScript(
        AutomationFramework.Cursor cursor,
        AutomationFramework.Keyboard keyboard,
        IAutomationVisionFactory visionFactory,
        ILogger<TestScript> logger)
        : base(logger)
    {
        _cursor = cursor;
        _keyboard = keyboard;
        _visionFactory = visionFactory;
    }

    public override string Name => "test-script";

    public override string Description => "Testing stuff";

    private readonly AutomationFramework.Cursor _cursor;
    private readonly AutomationFramework.Keyboard _keyboard;
    private readonly IAutomationVisionFactory _visionFactory;
    AutomationFramework.Vision _vision = null!;

    private Dictionary<string, VisionTemplateLease> _templates = new Dictionary<string, VisionTemplateLease>();


    protected override Task InitializeAsync(CancellationToken cancellationToken)
    {
        _vision = _visionFactory.Create();

        // Load templates
        AcquireTemplates
        (
            VisionTemplateFileNames.TSM_ENGINEERING_WINDOW,
            VisionTemplateFileNames.TSM_MAIL_WINDOW,
            VisionTemplateFileNames.AB_GILDED_TRADERS_BRUTOSAUR_BTN
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
        var searchRegion = Screen.PrimaryScreen?.Bounds;

        bool result = await MountBrutosaurAsync(searchRegion!.Value, cancellationToken: cancellationToken);

        _logger.LogInformation("Mounting brutosaur success: {}", result);
    }

    private void AcquireTemplates(params string[] filenames)
    {
        ArgumentNullException.ThrowIfNull(filenames);

        foreach (var filename in filenames)
        {
            _templates[filename] = _vision.AcquireTemplateLease(filename);
        }
    }

    private async Task<bool> MountBrutosaurAsync(Rectangle searchRegion, int maxAttempts = 3, CancellationToken cancellationToken = default)
    {
        // buffs are always in the top half of the screen
        var buffSearchRegion = new Rectangle(searchRegion.X, searchRegion.Y, searchRegion.Width, (int)(searchRegion.Height / 4f));

        // action bars are always in the bottom half of the screen
        var abSearchRegion = new Rectangle(searchRegion.X, (int)(searchRegion.Y + searchRegion.Height * 0.75f), searchRegion.Width, (int)(searchRegion.Height / 4f));

        bool success = false;

        for (int attempt = 1; attempt <= maxAttempts; ++attempt)
        {
            // move mouse to center-ish with a random offset between -100 to 100
            var newPos = searchRegion.Center() + new Vector2(Random.Shared.Next(-100, 100), Random.Shared.Next(-100, 100));
            await _cursor.MoveToAsync(newPos);

            //find the gilded brutosaur on the action bar
            var mountAbImageMatch = await _vision.FindImageAsync(_templates[VisionTemplateFileNames.AB_GILDED_TRADERS_BRUTOSAUR_BTN].TemplateMat, 0.60, cancellationToken: cancellationToken, searchRegion: abSearchRegion);
            if (mountAbImageMatch is null)
            {
                _logger.LogWarning("GildedTradersBrutosaur not found.");
                continue;
            }

            var gildedTradersBrutosaurRandomPoint = mountAbImageMatch.ToGlobalBounds().Inset(20).GetRandomPointInBounds();
            await _cursor.MoveToAsync(gildedTradersBrutosaurRandomPoint, cancellationToken: cancellationToken);
            await Task.Delay(TimeSpan.FromSeconds(1).ApplyRandomFactor(), cancellationToken: cancellationToken);
            await _cursor.ClickAsync(cancellationToken: cancellationToken);

            // wait for mount to be summoned
            await Task.Delay(TimeSpan.FromSeconds(3).ApplyRandomFactor(0.8, 1.2), cancellationToken);

            //assert the mount is found in the buff list
            var mountBuffImageMatch = await _vision.FindImageAsync(_templates[VisionTemplateFileNames.AB_GILDED_TRADERS_BRUTOSAUR_BTN].TemplateMat, 0.60, cancellationToken: cancellationToken, searchRegion: buffSearchRegion);
            
            if (mountBuffImageMatch is not null)
            {
                // the buff is found, therefore we are mounted on the brutosaur
                success = true;
                break;
            }
        }


        return success;
    }
}