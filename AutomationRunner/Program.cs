using AutomationRunner.Scripting;
using AutomationRunner.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.CommandLine;

namespace AutomationRunner;

internal static class Program
{
    private static int Main(string[] args)
    {
        using var host = BuildHost(args);
        var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("AutomationRunner.Program");

        var rootCommand = new RootCommand("AutomationRunner script runner")
        {
            CreateRunCommand(host.Services, logger),
            CreateListCommand(host.Services, logger)
        };

        return rootCommand.Parse(args).Invoke();
    }

    private static Command CreateRunCommand(IServiceProvider services, ILogger logger)
    {
        var scriptNameArgument = new Argument<string>("script-name")
        {
            Description = "The script name to run."
        };

        var runCommand = new Command("run", "Run a script by name.")
        {
            scriptNameArgument
        };
        
        runCommand.SetAction(parseResult =>
        {
            var requestedScriptName = parseResult.GetValue(scriptNameArgument);
            if (string.IsNullOrWhiteSpace(requestedScriptName))
            {
                logger.LogWarning("Script name is required.");
                return 1;
            }

            return HandleRunCommandAsync(requestedScriptName, services, logger, CancellationToken.None)
                .GetAwaiter()
                .GetResult();
        });

        return runCommand;
    }

    private static Command CreateListCommand(IServiceProvider services, ILogger logger)
    {
        var listCommand = new Command("list", "List scripts, then choose one to run or exit.");
        listCommand.SetAction(_ =>
            HandleListCommandAsync(services, logger, CancellationToken.None)
                .GetAwaiter()
                .GetResult());

        return listCommand;
    }

    private static async Task<int> HandleRunCommandAsync(
        string scriptName,
        IServiceProvider services,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var scripts = GetScriptInfos(services);
        if (scripts.Count == 0)
        {
            logger.LogError("No scripts were found. Add classes in the Scripts folder that implement IAutomationScript.");
            return 1;
        }

        var selectedScript = ResolveByName(scriptName, scripts);
        if (selectedScript is null)
        {
            logger.LogWarning("Script '{ScriptName}' was not found.", scriptName);
            return 1;
        }

        return await RunScriptAsync(selectedScript.Value.Name, services, logger, cancellationToken);
    }

    private static async Task<int> HandleListCommandAsync(
        IServiceProvider services,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var scripts = GetScriptInfos(services);
        if (scripts.Count == 0)
        {
            logger.LogError("No scripts were found. Add classes in the Scripts folder that implement IAutomationScript.");
            return 1;
        }

        while (true)
        {
            var selection = PromptForScriptSelection(scripts);
            if (selection.ExitRequested)
            {
                logger.LogInformation("Exiting.");
                return 0;
            }

            if (selection.Script is null)
            {
                logger.LogWarning("No script selected.");
                continue;
            }

            var selectedScript = selection.Script.Value;
            return await RunScriptAsync(selectedScript.Name, services, logger, cancellationToken);
        }
    }

    private static async Task<int> RunScriptAsync(
        string scriptName,
        IServiceProvider services,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        using var cancellationTokenSource = new CancellationTokenSource();
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationTokenSource.Token, cancellationToken);
        using var scope = services.CreateScope();

        void OnCancelKeyPress(object? _, ConsoleCancelEventArgs eventArgs)
        {
            eventArgs.Cancel = true;
            cancellationTokenSource.Cancel();
        }

        Console.CancelKeyPress += OnCancelKeyPress;

        var scripts = scope.ServiceProvider.GetServices<IAutomationScript>();
        var selectedScript = scripts.FirstOrDefault(script =>
            string.Equals(script.Name, scriptName, StringComparison.OrdinalIgnoreCase));

        if (selectedScript is null)
        {
            logger.LogError("Script '{ScriptName}' is registered in menu but could not be resolved from DI.", scriptName);
            return 1;
        }

        logger.LogInformation("Running script: {ScriptName}", selectedScript.Name);
        logger.LogInformation("Press Ctrl+C to stop.");

        try
        {
            await selectedScript.ExecuteAsync(linkedCts.Token);
            logger.LogInformation("Script completed.");
            return 0;
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation("Script canceled.");
            return 2;
        }
        finally
        {
            Console.CancelKeyPress -= OnCancelKeyPress;

            try
            {
                selectedScript.Dispose();
            }
            catch (Exception disposeException)
            {
                logger.LogWarning(disposeException, "Script cleanup failed for {ScriptName}.", selectedScript.Name);
            }
        }
    }

    private static IHost BuildHost(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);

        builder.Configuration.Sources.Clear();
        builder.Configuration
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
            .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: false)
            .AddEnvironmentVariables();

        builder.Logging.ClearProviders();
        builder.Logging
            .AddConfiguration(builder.Configuration.GetSection("Logging"))
            .AddSimpleConsole(options =>
            {
                options.SingleLine = true;
                options.TimestampFormat = "HH:mm:ss ";
                options.IncludeScopes = false;
            });

        builder.Services.AddTransient<AutomationFramework.Cursor>();
        builder.Services.AddTransient<AutomationFramework.Keyboard>();
        builder.Services.AddSingleton<IAutomationVisionFactory, AutomationVisionFactory>();
        builder.Services.AddDiscoveredScripts();

        return builder.Build();
    }

    private static IReadOnlyList<ScriptInfo> GetScriptInfos(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var scripts = scope.ServiceProvider.GetServices<IAutomationScript>()
            .Select(script => new ScriptInfo(script.Name, script.Description))
            .OrderBy(script => script.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var duplicateNames = scripts
            .GroupBy(script => script.Name, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToArray();

        if (duplicateNames.Length > 0)
        {
            throw new InvalidOperationException($"Duplicate script names found: {string.Join(", ", duplicateNames)}");
        }

        return scripts;
    }

    private static ScriptSelection PromptForScriptSelection(IReadOnlyList<ScriptInfo> scripts)
    {
        PrintScriptList(scripts);
        Console.Write("Select a script by number or name (or type 'exit'): ");

        var input = Console.ReadLine();
        if (string.IsNullOrWhiteSpace(input))
        {
            return new ScriptSelection(null, false);
        }

        var selector = input.Trim();
        if (string.Equals(selector, "exit", StringComparison.OrdinalIgnoreCase)
            || string.Equals(selector, "quit", StringComparison.OrdinalIgnoreCase)
            || string.Equals(selector, "q", StringComparison.OrdinalIgnoreCase)
            || string.Equals(selector, "x", StringComparison.OrdinalIgnoreCase))
        {
            return new ScriptSelection(null, true);
        }

        var script = ResolveByNameOrIndex(selector, scripts);
        if (script is null)
        {
            Console.WriteLine($"Script '{input}' was not found.");
        }

        return new ScriptSelection(script, false);
    }

    private static ScriptInfo? ResolveByName(string scriptName, IReadOnlyList<ScriptInfo> scripts)
    {
        foreach (var script in scripts)
        {
            if (string.Equals(script.Name, scriptName, StringComparison.OrdinalIgnoreCase))
            {
                return script;
            }
        }

        return null;
    }

    private static ScriptInfo? ResolveByNameOrIndex(string selector, IReadOnlyList<ScriptInfo> scripts)
    {
        if (int.TryParse(selector, out var scriptNumber))
        {
            var index = scriptNumber - 1;
            if (index >= 0 && index < scripts.Count)
            {
                return scripts[index];
            }

            Console.WriteLine($"Script number '{scriptNumber}' is out of range.");
            return null;
        }

        foreach (var script in scripts)
        {
            if (string.Equals(script.Name, selector, StringComparison.OrdinalIgnoreCase))
            {
                return script;
            }
        }

        return null;
    }

    private static void PrintScriptList(IReadOnlyList<ScriptInfo> scripts)
    {
        Console.WriteLine("Available scripts:");
        for (var index = 0; index < scripts.Count; index++)
        {
            var script = scripts[index];
            Console.WriteLine($"  {index + 1}. {script.Name} - {script.Description}");
        }
    }

    private readonly record struct ScriptInfo(string Name, string Description);
    private readonly record struct ScriptSelection(ScriptInfo? Script, bool ExitRequested);

}