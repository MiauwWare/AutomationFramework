

using System.Numerics;
using AutomationFramework;
using AutomationFramework.Extensions;
using AutomationRunner.Scripting;
using AutomationRunner.Services;
using Microsoft.Extensions.Logging;
using OpenCvSharp;

namespace AutomationRunner.Scripts;

public sealed class VisionShadesCrafting : BaseScript
{
    public VisionShadesCrafting(
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

    public override string Name => "vision-shades-crafting";

    public override string Description => "Uses vision to find the correct buttons for wrist crafting and clicks them.";

    private readonly AutomationFramework.Cursor _cursor;
    private readonly AutomationFramework.Keyboard _keyboard;
    private readonly IAutomationVisionFactory _visionFactory;
    AutomationFramework.Vision _vision = null!;

    Dictionary<string, Mat> _templates = new Dictionary<string, Mat>();


    protected override Task InitializeAsync(CancellationToken cancellationToken)
    {
        _vision = _visionFactory.Create();

        // Load templates
        AcquireTemplates
        (
            VisionTemplateFileNames.AB_ENGINEERING_BTN,
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

    protected override async Task RunAsync(CancellationToken cancellationToken)
    {
        var searchRegion = Screen.PrimaryScreen?.Bounds;

        while (cancellationToken.IsCancellationRequested == false)
        {
            // Open crafting window
            var engineeringButtonImageMatch = await _vision.FindImageAsync(_templates[VisionTemplateFileNames.AB_ENGINEERING_BTN], 0.7, cancellationToken: cancellationToken, searchRegion: searchRegion);
            if (engineeringButtonImageMatch is null)
            {
                Logger.LogWarning("Engineering button not found.");
                return;
            }

            var engineeringButtonRandomPoint = engineeringButtonImageMatch.ToGlobalBounds().Inset(20).GetRandomPointInBounds();
            await _cursor.MoveToAsync(engineeringButtonRandomPoint, cancellationToken: cancellationToken);
            await _cursor.ClickAsync();


            // Wait for window to open
            await Task.Delay(TimeSpan.FromSeconds(Random.Shared.NextFloat(2, 3)), cancellationToken);

            // Click TSM max button
            var maxButtonImageMatch = await _vision.FindImageAsync(_templates[VisionTemplateFileNames.TSM_MAX_BTN], 0.7, cancellationToken: cancellationToken, searchRegion: searchRegion);
            if (maxButtonImageMatch == null)
            {
                Logger.LogWarning("Max button not found.");
                return;
            }

            var maxButtonBounds = maxButtonImageMatch.ToGlobalBounds();
            maxButtonBounds = maxButtonBounds.Translate(-3 * maxButtonBounds.Width, 0);

            var quantityInputRandomPoint = maxButtonBounds.GetRandomPointInBounds();
            await _cursor.MoveToAsync(quantityInputRandomPoint, cancellationToken: cancellationToken);
            await _cursor.ClickAsync();

            // input the quantity to craft, which is 200 in this case
            await _keyboard.TypeTextAsync("200", cancellationToken: cancellationToken);

            // Click TSM craft button
            var craftButtonImageMatch = await _vision.FindImageAsync(_templates[VisionTemplateFileNames.TSM_CRAFT_BTN], 0.8, cancellationToken: cancellationToken, searchRegion: searchRegion);
            if (craftButtonImageMatch == null)
            {
                Logger.LogWarning("Craft button not found.");
                return;
            }

            var tsmCraftButtonRandomPoint = craftButtonImageMatch.ToGlobalBounds().Padd(30, 5).GetRandomPointInBounds();
            await _cursor.MoveToAsync(tsmCraftButtonRandomPoint, cancellationToken: cancellationToken);
            await _cursor.ClickAsync();


            // wait for crafts to be complete about 100 sec
            await Task.Delay(TimeSpan.FromSeconds(175).ApplyRandomFactor(0.9, 1), cancellationToken);

            // Click TSM close button
            var tsmCloseButtonImageMatch = await _vision.FindImageAsync(_templates[VisionTemplateFileNames.TSM_CLOSE_BTN], 0.70, cancellationToken: cancellationToken, searchRegion: searchRegion);
            if (tsmCloseButtonImageMatch == null)
            {
                Logger.LogWarning("TSM close button not found.");
                return;
            }


            var tsmCloseButtonRandomPoint = tsmCloseButtonImageMatch.ToGlobalBounds().Inset(3).GetRandomPointInBounds();
            await _cursor.MoveToAsync(tsmCloseButtonRandomPoint, cancellationToken: cancellationToken);
            await _cursor.ClickAsync();

            // wait just a second for a possible last craft to complete
            await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);

            // mount up on the gilded brutosaur
            var gildedTradersBrutosaurImageMatch = await _vision.FindImageAsync(_templates[VisionTemplateFileNames.AB_GILDED_TRADERS_BRUTOSAUR_BTN], 0.60, cancellationToken: cancellationToken, searchRegion: searchRegion);
            if (gildedTradersBrutosaurImageMatch == null)
            {
                Logger.LogWarning("GildedTradersBrutosaur not found.");
                return;
            }

            var gildedTradersBrutosaurRandomPoint = gildedTradersBrutosaurImageMatch.ToGlobalBounds().Inset(20).GetRandomPointInBounds();
            await _cursor.MoveToAsync(gildedTradersBrutosaurRandomPoint, cancellationToken: cancellationToken);
            await _cursor.ClickAsync();

            // wait for mount to be summoned
            await Task.Delay(TimeSpan.FromSeconds(3).ApplyRandomFactor(0.8, 1.2), cancellationToken);

            // target the mail NPC via macro
            var targetMailButtonImageMatch = await _vision.FindImageAsync(_templates[VisionTemplateFileNames.AB_TARGET_MAIL_NPC_BTN], 0.70, cancellationToken: cancellationToken, searchRegion: searchRegion);
            if (targetMailButtonImageMatch == null)
            {
                Logger.LogWarning("TargetMailButton not found.");
                return;
            }

            var targetMailButtonRandomPoint = targetMailButtonImageMatch.ToGlobalBounds().Inset(20).GetRandomPointInBounds();
            await _cursor.MoveToAsync(targetMailButtonRandomPoint, cancellationToken: cancellationToken);
            await _cursor.ClickAsync();

            // simulate human brain processing time xD
            await Task.Delay(TimeSpan.FromSeconds(1).ApplyRandomFactor(0.8, 1.2), cancellationToken);

            // interact with the mail NPC (with the interact keybind)
            await _keyboard.PressKeyAsync(VirtualKey.F, cancellationToken: cancellationToken);

            // wait for mail window to open
            await Task.Delay(TimeSpan.FromSeconds(0.5), cancellationToken);

            // click the TSM mailbox groups button
            var tsmMailboxGroupsImageMatch = await _vision.FindImageAsync(_templates[VisionTemplateFileNames.TSM_MAILBOX_GROUPS_BTN], 0.7, cancellationToken: cancellationToken, searchRegion: searchRegion);
            if (tsmMailboxGroupsImageMatch == null)
            {
                Logger.LogWarning("Groups button not found.");
                return;
            }

            var tsmMailboxGroupsButtonRandomPoint = tsmMailboxGroupsImageMatch.ToGlobalBounds().Inset(10).GetRandomPointInBounds();
            await _cursor.MoveToAsync(tsmMailboxGroupsButtonRandomPoint, cancellationToken: cancellationToken);
            await _cursor.ClickAsync();

            //wait 1 sec, to ensure tab switch
            await Task.Delay(TimeSpan.FromSeconds(1));

            // click the TSM mail selected groups button
            var tsmMailSelectedGroupsImageMatch = await _vision.FindImageAsync(_templates[VisionTemplateFileNames.TSM_MAIL_SELECTED_GROUPS_BTN], 0.50, cancellationToken: cancellationToken, searchRegion: searchRegion);
            if (tsmMailSelectedGroupsImageMatch == null)
            {
                Logger.LogWarning("Mail Selected Groups button not found.");
                return;
            }

            var tsmMailSelectedGroupsButtonRandomPoint = tsmMailSelectedGroupsImageMatch.ToGlobalBounds().Padd(100, 5).GetRandomPointInBounds();
            await _cursor.MoveToAsync(tsmMailSelectedGroupsButtonRandomPoint, cancellationToken: cancellationToken);
            await _cursor.ClickAsync();

            // wait for all mail to send
            await Task.Delay(TimeSpan.FromSeconds(16).ApplyRandomFactor(0.8, 1.2), cancellationToken);
        }
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