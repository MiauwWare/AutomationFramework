using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace AutomationRunner.Scripting;

public static class ScriptRegistry
{
    public static IReadOnlyList<Type> DiscoverScriptTypes()
    {
        return Assembly
            .GetExecutingAssembly()
            .GetTypes()
            .Where(type => !type.IsAbstract && !type.IsInterface)
            .Where(type => typeof(IAutomationScript).IsAssignableFrom(type))
            .OrderBy(type => type.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static IServiceCollection AddDiscoveredScripts(this IServiceCollection services)
    {
        foreach (var scriptType in DiscoverScriptTypes())
        {
            services.AddTransient(typeof(IAutomationScript), scriptType);
        }

        return services;
    }
}
