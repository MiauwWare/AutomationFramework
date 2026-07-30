using System;
using System.ComponentModel;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using AutomationFramework;
using AutomationFramework.Extensions;
using AutomationRunner.Scripting;
using AutomationRunner.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AutomationRunner.Scripts;

public class WoWAuthenticate : BaseScript
{
    public WoWAuthenticate
    (
        ILogger<WoWAuthenticate> logger, 
        IConfiguration config,
        IAutomationVisionFactory visionFactory,
        AutomationFramework.Cursor cursor,
        Keyboard keyboard
    ) 
    : base(logger)
    {
        _visionFactory = visionFactory;
        _cursor = cursor;
        _keyboard = keyboard;
        _config = config;
    }

    public override string Name => "wow-authenticate";

    public override string Description => "Authenticate into wow with credentials stored in the appsettings.";

    private readonly Keyboard _keyboard;
    private readonly AutomationFramework.Cursor _cursor;
    private readonly IAutomationVisionFactory _visionFactory;
    private readonly IConfiguration _config;
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
            VisionTemplateFileNames.MAIN_MENU_PASSWORD_TEXT,
            VisionTemplateFileNames.MAIN_MENU_BUTTONS,
            VisionTemplateFileNames.CHARACTER_SELECT_CREATE_DELETE_RESTORE_BUTTONS
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
        
        await ThrowIfMainMenuButtonsNotFoundAsync(cancellationToken);

        // find password text
        var passwordMatch = await _vision.FindImageAsync(_templateLeases[VisionTemplateFileNames.MAIN_MENU_PASSWORD_TEXT].TemplateMat, 0.6);

        if (passwordMatch is null)
        {
            throw new InvalidOperationException("Failed to find the password text on the main menu. Cannot proceed with login.");
        }

        // move cursor to password box and highlight to enter password
        var passwordBoxPos = passwordMatch.ToGlobalBounds().Translate(0, (int)(passwordMatch.Bounds.Height * 1.5f)).Center();

        await _cursor.MoveToAsync(passwordBoxPos, cancellationToken: cancellationToken);
        await Task.Delay(500);

        await _cursor.ClickAsync();

        //clear all current text
        await _keyboard.PressChordAsync([VirtualKey.Control, VirtualKey.Backspace], cancellationToken: cancellationToken);
        await Task.Delay(500);

        //get password from config
        var password = _config.GetRequiredSection("WoWPassword").Get<string>()!;
        
        //type in the password
        await _keyboard.TypeTextAsync(password);

        //press enter to log-in
        await _keyboard.PressKeyAsync(VirtualKey.Enter);

        //wait for a bit to let the login process complete
        await Task.Delay(TimeSpan.FromSeconds(10));


        //confirm authentication was successful by checking for the character select menu buttons
        await ThrowIfCharacterSelectButtonsNotFoundAsync(cancellationToken);
    }


    private async Task ThrowIfMainMenuButtonsNotFoundAsync(CancellationToken cancellationToken, int maxAttempts = 3)
    {
        ArgumentNullException.ThrowIfNull(_vision);

        await Retry.ExecuteAsync
        (
            async () =>
            {
                var match = await _vision.FindImageAsync
                (
                    _templateLeases[VisionTemplateFileNames.MAIN_MENU_BUTTONS].TemplateMat,
                    0.6,
                    cancellationToken: cancellationToken
                );

                if (match == null)
                {
                    throw new InvalidOperationException("Failed to find the main menu buttons on the WoW login screen.");
                }
            },
            maxAttempts,
            TimeSpan.FromSeconds(5),
            cancellationToken
        );
    }


    private async Task ThrowIfCharacterSelectButtonsNotFoundAsync(CancellationToken cancellationToken, int maxAttempts = 3)
    {
        ArgumentNullException.ThrowIfNull(_vision);

        await Retry.ExecuteAsync
        (
            async () =>
            {
                var match = await _vision.FindImageAsync
                (
                    _templateLeases[VisionTemplateFileNames.CHARACTER_SELECT_CREATE_DELETE_RESTORE_BUTTONS].TemplateMat,
                    0.6,
                    cancellationToken: cancellationToken
                );

                if (match == null)
                {
                    throw new InvalidOperationException("Failed to find the character select buttons on the WoW character selection screen. Authentication failed.");
                }
            },
            maxAttempts,
            TimeSpan.FromSeconds(5),
            cancellationToken
        );
    }
}
