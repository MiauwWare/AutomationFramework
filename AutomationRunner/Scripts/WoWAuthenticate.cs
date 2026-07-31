using AutomationFramework;
using AutomationFramework.Extensions;
using AutomationFramework.Templates;
using AutomationFramework.VisionModels;
using AutomationRunner.Scripting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AutomationRunner.Scripts;

/// <summary>Signs in to World of Warcraft using template matching and the configured password.</summary>
public sealed class WoWAuthenticate : BaseScript
{
    private const double LoginConfidence = .3;
    private static readonly VisionSearchOptions LoginSearch = new() { MinimumConfidence = LoginConfidence };

    private readonly IVisionTemplateResourceManager _templateManager;
    private readonly AutomationFramework.Cursor _cursor;
    private readonly Keyboard _keyboard;
    private readonly IConfiguration _configuration;
    private Vision? _vision;
    private Dictionary<string, VisionTemplateLease>? _templates;

    public WoWAuthenticate(
        ILogger<WoWAuthenticate> logger,
        IConfiguration configuration,
        IVisionTemplateResourceManager templateManager,
        AutomationFramework.Cursor cursor,
        Keyboard keyboard)
        : base(logger)
    {
        _configuration = configuration;
        _templateManager = templateManager;
        _cursor = cursor;
        _keyboard = keyboard;
    }

    public override string Name => "wow-authenticate";
    public override string Description => "Authenticates into WoW with the password configured in appsettings.";

    protected override Task InitializeAsync(CancellationToken cancellationToken)
    {
        _vision = new Vision(_templateManager);
        var templateFileNames = new[]
        {
            VisionTemplateFileNames.MAIN_MENU_PASSWORD_TEXT,
            VisionTemplateFileNames.MAIN_MENU_BUTTONS,
            VisionTemplateFileNames.CHARACTER_SELECT_CREATE_DELETE_RESTORE_BUTTONS
        };

        _templates = _vision.AcquireTemplates(templateFileNames);

        return Task.CompletedTask;
    }

    protected override async Task RunAsync(CancellationToken cancellationToken)
    {
        var vision = RequireVision();
        await EnsureMainMenuButtonsAsync(cancellationToken);

        var passwordMatch = vision.Find(GetTemplate(VisionTemplateFileNames.MAIN_MENU_PASSWORD_TEXT), LoginSearch, cancellationToken)
            ?? throw new InvalidOperationException("Failed to find the password text on the WoW login screen.");

        // The input lies directly beneath its label; use the matched template's global coordinates for mouse input.
        var passwordBox = passwordMatch.GlobalBounds
            .Translate(0, (int)(passwordMatch.Bounds.Height * 1.5f))
            .Center();

        await _cursor.MoveToAsync(passwordBox, cancellationToken: cancellationToken);
        await Task.Delay(500, cancellationToken);
        await _cursor.ClickAsync(cancellationToken: cancellationToken);

        await _keyboard.PressChordAsync([VirtualKey.Control, VirtualKey.Backspace], cancellationToken: cancellationToken);
        await Task.Delay(500, cancellationToken);

        var password = _configuration.GetRequiredSection("WoWPassword").Get<string>()
            ?? throw new InvalidOperationException("WoWPassword must be configured.");
        await _keyboard.TypeTextAsync(password, cancellationToken: cancellationToken);
        await _keyboard.PressKeyAsync(VirtualKey.Enter, cancellationToken: cancellationToken);

        await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);
        await EnsureCharacterSelectButtonsAsync(cancellationToken);
    }

    public override void Dispose()
    {
        if (_templates is not null)
        {
            foreach (var lease in _templates.Values) lease.Dispose();
            _templates.Clear();
        }
        _vision?.Dispose();
    }

    private async Task EnsureMainMenuButtonsAsync(CancellationToken cancellationToken)
    {
        await Retry.ExecuteAsync(() =>
        {
            var vision = RequireVision();

            var match = vision.Find(GetTemplate(VisionTemplateFileNames.MAIN_MENU_BUTTONS), LoginSearch, cancellationToken);
            if (match is null)
            {
                throw new InvalidOperationException("Failed to find the main menu buttons on the WoW login screen.");
            }

            return Task.CompletedTask;
        }, 3, TimeSpan.FromSeconds(5), cancellationToken);
    }

    private async Task EnsureCharacterSelectButtonsAsync(CancellationToken cancellationToken)
    {
        await Retry.ExecuteAsync
        (
            () =>
            {
                var vision = RequireVision();

                var match = vision.Find(GetTemplate(VisionTemplateFileNames.CHARACTER_SELECT_CREATE_DELETE_RESTORE_BUTTONS), LoginSearch, cancellationToken);
                if (match is null)
                {
                    throw new InvalidOperationException("Failed to find character-select buttons. Authentication failed.");
                }

                return Task.CompletedTask;
            },
            3,
            TimeSpan.FromSeconds(5),
            cancellationToken
        );
    }

    private Vision RequireVision() => _vision ?? throw new InvalidOperationException("Vision is not initialized.");

    private VisionTemplateLease GetTemplate(string name) => _templates?.GetValueOrDefault(name)
        ?? throw new InvalidOperationException("Template leases are not initialized.");

}
