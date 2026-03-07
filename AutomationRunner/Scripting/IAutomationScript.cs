namespace AutomationRunner.Scripting;

public interface IAutomationScript : IDisposable
{
    string Name { get; }

    string Description { get; }


    Task InitializeAsync(ScriptExecutionContext context, CancellationToken cancellationToken) => Task.CompletedTask;

    Task ExecuteAsync(ScriptExecutionContext context, CancellationToken cancellationToken);
}
