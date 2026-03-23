namespace AutomationRunner.Scripting;

public interface IAutomationScript : IDisposable
{
    string Name { get; }

    string Description { get; }

    Task ExecuteAsync(CancellationToken cancellationToken);
}
