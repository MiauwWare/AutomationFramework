

using System.Numerics;
using AutomationFramework;
using AutomationFramework.Extensions;
using AutomationRunner.Scripting;
using Microsoft.Extensions.Configuration;
using OpenCvSharp;

namespace AutomationRunner.Scripts;

public sealed class TypingTest : BaseScript
{
    public override string Name => "typing-test";

    public override string Description => "Automates typing and keyboard input.";

    AutomationFramework.Keyboard _keyboard = new();

    protected override Task InitializeAsync(ScriptExecutionContext context, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public override void Dispose()
    {   
    
    }

    protected override async Task RunAsync(ScriptExecutionContext context, CancellationToken cancellationToken)
    {
        await _keyboard.TypeTextAsync("""Lorem ipsum dolor sit amet, consectetur adipiscing elit. Donec arcu leo, hendrerit non tortor ac, ultrices vestibulum nisi. Fusce vel volutpat lacus. Maecenas consectetur mattis pellentesque. Donec cursus ipsum sit amet justo imperdiet, at rutrum felis auctor. Aliquam vitae finibus dolor. Proin faucibus urna non massa viverra, ac ultricies diam sodales. Donec aliquam rhoncus massa, quis porttitor velit cursus vitae. Pellentesque ac ante sit amet enim sodales fringilla sed at libero. Sed tempor fringilla tempus. Quisque sagittis metus in mattis sagittis. Quisque in laoreet felis. Proin ac magna ex. Curabitur dapibus non purus vel malesuada.""");
    }   
}