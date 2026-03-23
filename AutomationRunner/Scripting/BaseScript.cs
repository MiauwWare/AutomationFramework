namespace AutomationRunner.Scripting;

using Microsoft.Extensions.Logging;

public abstract class BaseScript : IAutomationScript
{
    protected BaseScript(ILogger logger)
    {
        _logger = logger;
    }

    public abstract string Name { get; }

    public abstract string Description { get; }

    protected readonly ILogger _logger;

    private bool _initialized = false;
    
    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        TimeSpan startDelay = TimeSpan.FromSeconds(2);
        _logger.LogInformation("Starting script: {ScriptName} in {StartDelaySeconds} seconds...", Name, startDelay.TotalSeconds);
        await Task.Delay(startDelay, cancellationToken);

        if (_initialized == false)
        {
            await InitializeAsync(cancellationToken);
            _initialized = true;
        }

        await RunAsync(cancellationToken); 
    }


    protected abstract Task InitializeAsync(CancellationToken cancellationToken);

    protected abstract Task RunAsync(CancellationToken cancellationToken);
    
    public abstract void Dispose();
}