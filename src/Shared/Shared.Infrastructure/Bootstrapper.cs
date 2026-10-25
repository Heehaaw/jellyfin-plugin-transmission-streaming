namespace Shared.Infrastructure;

using Microsoft.Extensions.DependencyInjection;
using Packaging;

/// <summary>Registers infrastructure shared by the independently installed plugins.</summary>
public static class Bootstrapper
{
    /// <summary>Adds the installed-package metadata reader.</summary>
    public static IServiceCollection AddSharedInfrastructure(this IServiceCollection services)
    {
        return services.AddSingleton<IPluginReleaseMetadata, PluginReleaseMetadata>();
    }
}
