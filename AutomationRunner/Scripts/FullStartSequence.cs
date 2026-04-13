using AutomationRunner.Scripting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AutomationRunner.Scripts;

public sealed class FullStartSequence : BaseScript
{
	private static readonly string[] StepNames =
	[
		"start-wow",
		"wow-authenticate",
		"wow-select-character",
		"wow-wait-until-entered-world"
	];

	private readonly IServiceScopeFactory _scopeFactory;

	public FullStartSequence(ILogger<FullStartSequence> logger, IServiceScopeFactory scopeFactory)
		: base(logger)
	{
		_scopeFactory = scopeFactory;
	}

	public override string Name => "wow-full-start-sequence";

	public override string Description => "Runs the full WoW startup flow.";

	protected override Task InitializeAsync(CancellationToken cancellationToken)
	{
		return Task.CompletedTask;
	}

	protected override async Task RunAsync(CancellationToken cancellationToken)
	{
		for (var i = 0; i < StepNames.Length; i++)
		{
			cancellationToken.ThrowIfCancellationRequested();

			var stepName = StepNames[i];
			_logger.LogInformation("Running startup step {StepIndex}/{TotalSteps}: {StepName}", i + 1, StepNames.Length, stepName);

			using var scope = _scopeFactory.CreateScope();
			var stepScript = scope.ServiceProvider
				.GetServices<IAutomationScript>()
				.FirstOrDefault(script => string.Equals(script.Name, stepName, StringComparison.OrdinalIgnoreCase));

			if (stepScript is null)
			{
				throw new InvalidOperationException($"Required startup step script '{stepName}' was not found.");
			}

			try
			{
				await stepScript.ExecuteAsync(cancellationToken);
			}
			finally
			{
				stepScript.Dispose();
			}
		}
	}

	public override void Dispose()
	{
	}

}
