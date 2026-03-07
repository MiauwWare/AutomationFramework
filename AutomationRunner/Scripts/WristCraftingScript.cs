using System.Numerics;
using AutomationFramework.Extensions;
using AutomationRunner.Scripting;

namespace AutomationRunner.Scripts;

public sealed class WristCraftingScript : BaseScript
{
    public override string Name => "wrist-crafting-script";

    public override string Description => "Crafts items using the wrist crafting method";


    private AutomationFramework.Cursor _cursor = new();
    private Vector2 _tailoringButtonPos = new Vector2(2264, 1309);
    private Vector2 _createAllPos = new Vector2(897, 913);
    private Vector2 _closeTailoringButtonPos = new Vector2(1176, 155);
    private Vector2 _mountButtonPos = new Vector2(1230, 1368);
    private Vector2 _mailNPCPos = new Vector2(1797, 833);
    private Vector2 _groupSectionPos = new Vector2(1342, 428);
    private Vector2 _sendMailButtonPos = new Vector2(1434, 987);
    private Vector2 _closeMailButtonPos = new Vector2(1753, 424);


    protected override Task InitializeAsync(ScriptExecutionContext context, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public override void Dispose()
    {
        
    }

    protected override async Task RunAsync(ScriptExecutionContext context, CancellationToken cancellationToken)
    {
        while (cancellationToken.IsCancellationRequested == false)
        {
            await _cursor.MoveToAsync(_tailoringButtonPos, cancellationToken: cancellationToken);
            await _cursor.ClickAsync(cancellationToken: cancellationToken);
            await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
            await _cursor.MoveToAsync(_createAllPos, cancellationToken: cancellationToken);
            await _cursor.ClickAsync(cancellationToken: cancellationToken);
            await Task.Delay(TimeSpan.FromSeconds(Random.Shared.Next(130, 170)), cancellationToken);
            
            
            await _cursor.MoveToAsync(_closeTailoringButtonPos, cancellationToken: cancellationToken);
            await _cursor.ClickAsync(cancellationToken: cancellationToken);
            await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
            await _cursor.MoveToAsync(_mountButtonPos, cancellationToken: cancellationToken);
            await _cursor.ClickAsync(cancellationToken: cancellationToken);
            await Task.Delay(TimeSpan.FromSeconds(4), cancellationToken);
            await _cursor.MoveToAsync(_mailNPCPos, cancellationToken: cancellationToken);
            await _cursor.ClickAsync(AutomationFramework.MouseButton.Right, cancellationToken: cancellationToken);
            await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
            await _cursor.MoveToAsync(_groupSectionPos, cancellationToken: cancellationToken);
            await _cursor.ClickAsync(cancellationToken: cancellationToken);
            await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
            await _cursor.MoveToAsync(_sendMailButtonPos, cancellationToken: cancellationToken);
            await _cursor.ClickAsync(cancellationToken: cancellationToken);
            await Task.Delay(TimeSpan.FromSeconds(Random.Shared.Next(10, 15)), cancellationToken);
            await _cursor.MoveToAsync(_closeMailButtonPos, cancellationToken: cancellationToken);
            await _cursor.ClickAsync(cancellationToken: cancellationToken);
            await Task.Delay(TimeSpan.FromSeconds(Random.Shared.Next(5, 10)), cancellationToken);
        }
    }
}