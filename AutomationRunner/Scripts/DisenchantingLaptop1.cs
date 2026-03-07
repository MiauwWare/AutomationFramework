using System.Numerics;
using AutomationFramework.Extensions;
using AutomationRunner.Scripting;

namespace AutomationRunner.Scripts;

public sealed class DisenchantingLaptop1 : BaseScript
{
    public override string Name => "disenchanting-laptop-1";

    public override string Description => "Disenchants and opens mailbox";
    

    private AutomationFramework.Cursor _cursor = new();
    private RectangleF _disenchantButtonBounds = new RectangleF(659, 597, 225, 14); // Example bounds for the "Disenchant" button
    private RectangleF _mailboxBounds = new RectangleF(133, 482, 100, 8); // Example bounds for the mailbox


    protected override Task InitializeAsync(ScriptExecutionContext context, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public override void Dispose()
    {
        
    }

    protected override async Task RunAsync(ScriptExecutionContext context, CancellationToken cancellationToken)
    {
        while (true)
        {
            int iterations = Random.Shared.Next(50, 100);

            for (int i = 0; i < iterations; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var target = GetRandomPointInBounds(_disenchantButtonBounds);
                await _cursor.MoveToAsync(target, cancellationToken: cancellationToken);
                await _cursor.ClickAsync(cancellationToken: cancellationToken);

                // Wait a random time between clicks to mimic human behavior
                await Task.Delay(Random.Shared.Next(2500, 3500), cancellationToken);
            }

            // After clicking the "Disenchant" button several times, click the mailbox
            var mailboxTarget = GetRandomPointInBounds(_mailboxBounds);
            await _cursor.MoveToAsync(mailboxTarget, cancellationToken: cancellationToken);
            await _cursor.ClickAsync(cancellationToken: cancellationToken);
        }
    }

    private Vector2 GetRandomPointInBounds(RectangleF bounds)
    {
        var x = (float)(bounds.X + Random.Shared.NextFloat(0f, bounds.Width));
        var y = (float)(bounds.Top + Random.Shared.NextFloat(0f, bounds.Height));
        return new Vector2(x, y);
    }

    
}
