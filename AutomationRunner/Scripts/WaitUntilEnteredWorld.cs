using System;
using AutomationFramework;
using AutomationFramework.Extensions;
using AutomationRunner.Scripting;
using AutomationRunner.Services;
using Microsoft.Extensions.Logging;

namespace AutomationRunner.Scripts;

public class WaitUntilEnteredWorld : BaseScript
{
    public WaitUntilEnteredWorld 
    (
        ILogger<WaitUntilEnteredWorld> logger, 
        IAutomationVisionFactory visionFactory
    ) 
    : base(logger)
    {
        _visionFactory = visionFactory;
    }

    public override string Name => "wow-wait-until-entered-world";

    public override string Description => "Wait until character has fully entered the world.";

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
            VisionTemplateFileNames.AB_TARGET_MAIL_NPC_BTN
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
        
        _logger.LogInformation("Waiting until player entered world by looking for action bar buttons...");

        bool ready = await WaitForActionBarButtonVisibleAsync(cancellationToken);

        if (ready == false)
        {
            throw new InvalidOperationException("Could not find action bar button after waiting for it.");
        }

        _logger.LogInformation("Entering world Complete.");
    }


    private async Task<bool> WaitForActionBarButtonVisibleAsync(CancellationToken cancellationToken, int maxAttempts = 10 )
    {
        ArgumentNullException.ThrowIfNull(_vision);

        for (var attempt = 1; attempt <= maxAttempts; ++attempt)
        {
            var targetMailButtonMatch = await _vision.FindImageAsync(
                _templateLeases[VisionTemplateFileNames.AB_TARGET_MAIL_NPC_BTN].TemplateMat,
                0.6,
                cancellationToken: cancellationToken);

            if (targetMailButtonMatch != null)
            {
                return true;
            }

            if (attempt < maxAttempts)
            {
                await Task.Delay(TimeSpan.FromSeconds(20), cancellationToken);
            }
        }

        return false;
    }
}

