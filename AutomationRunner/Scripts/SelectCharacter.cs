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
            VisionTemplateFileNames.CHARACTER_SELECT_ENTER_WORLD_BTN_ACTIVE
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
        
        _logger.LogInformation("Waiting for Enter World button to become active...");
        bool ready = await WaitEnterWorldButtonEnabledAsync(cancellationToken);

        if (ready == false)
        {
            throw new InvalidOperationException("Enter World button did not become active within the expected time.");
        }

        await _keyboard.PressKeyAsync(VirtualKey.Enter);

        _logger.LogInformation("Character Selection Complete.");
    }


    private async Task<bool> WaitEnterWorldButtonEnabledAsync(CancellationToken cancellationToken, int maxAttempts = 10 )
    {
        ArgumentNullException.ThrowIfNull(_vision);

        for (var attempt = 1; attempt <= maxAttempts; ++attempt)
        {
            var enterWorldButtonMatch = await _vision.FindImageAsync(
                _templateLeases[VisionTemplateFileNames.CHARACTER_SELECT_ENTER_WORLD_BTN_ACTIVE].TemplateMat,
                0.6,
                cancellationToken: cancellationToken);

            if (enterWorldButtonMatch != null)
            {
                return true;
            }

            if (attempt < maxAttempts)
            {
                await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
            }
        }

        return false;
    }
}
