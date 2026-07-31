using System;
using AutomationFramework;
using AutomationFramework.Extensions;
using AutomationRunner.Scripting;
using AutomationRunner.Services;
using Microsoft.Extensions.Logging;

namespace AutomationRunner.Scripts;

public class SelectCharacter : BaseScript
{
    public SelectCharacter 
    (
        ILogger<SelectCharacter> logger, 
        IAutomationVisionFactory visionFactory,
        Keyboard keyboard
    ) 
    : base(logger)
    {
        _visionFactory = visionFactory;
        _keyboard = keyboard;
    }

    public override string Name => "wow-select-character";

    public override string Description => "Select and enter world with a character.";

    private readonly Keyboard _keyboard;
    private readonly IAutomationVisionFactory _visionFactory;
    private Vision? _vision = null;

    private Dictionary<string, VisionTemplateLease> _templateLeases = null!;

    public override void Dispose()
    {
        // Release templates
        if (_templateLeases != null)
        {
            foreach (var templateLease in _templateLeases.Values)
            {
                templateLease?.Dispose();
            }

            _templateLeases.Clear();
        }
        
        _vision?.Dispose();
    }

    protected override Task InitializeAsync(CancellationToken cancellationToken)
    {
        _vision = _visionFactory.Create();
        
        _templateLeases = _vision.AcquireTemplateLeases(
            VisionTemplateFileNames.CHARACTER_SELECT_ENTER_WORLD_BTN_ACTIVE,
            VisionTemplateFileNames.UI_CHAT_BUBBLE_BUTTON
        );


        return Task.CompletedTask;
    }

    protected override async Task RunAsync(CancellationToken cancellationToken)
    {
        if (_vision == null)
        {
            throw new InvalidOperationException("Vision system is not initialized.");
        }

        if (_templateLeases == null)
        {
            throw new InvalidOperationException("Template leases are not initialized.");
        }
        
        // enter the world
        await EnterWorldAsync(cancellationToken: cancellationToken);


        //wait until loading screen is finished
        await WaitUntilLoadedInAsync(cancellationToken: cancellationToken);

    }


    private async Task EnterWorldAsync(CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(_vision);
        ArgumentNullException.ThrowIfNull(_keyboard);

        _logger.LogInformation("Entering World...");

        await Retry.ExecuteAsync
        (
            async () =>
            {
                var match = await _vision.FindImageAsync
                (
                    _templateLeases[VisionTemplateFileNames.CHARACTER_SELECT_ENTER_WORLD_BTN_ACTIVE].TemplateMat,
                    0.6,
                    cancellationToken: cancellationToken
                );

                if (match == null)
                {
                    throw new InvalidOperationException("Failed to find the active Enter World button on the WoW character selection screen. Cannot proceed to enter world.");
                }
            },
            maxAttempts: 3,
            delay: TimeSpan.FromSeconds(5),
            cancellationToken: cancellationToken
        );

        await _keyboard.PressKeyAsync(VirtualKey.Enter);
    }

    private async Task WaitUntilLoadedInAsync(CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(_vision);

        await Retry.ExecuteAsync
        (
            async () =>
            {
                var match = await _vision.FindImageAsync
                (
                    _templateLeases[VisionTemplateFileNames.UI_CHAT_BUBBLE_BUTTON].TemplateMat,
                    0.90,
                    cancellationToken: cancellationToken
                );

                if (match == null)
                {
                    throw new InvalidOperationException("Failed to find the chat bubble button on the WoW screen. Player may not have loaded into the world yet.");
                }
            },
            maxAttempts: 15,
            delay: TimeSpan.FromSeconds(10),
            cancellationToken: cancellationToken
        );

        _logger.LogInformation("Loading Screen Finished!");
    }
}
