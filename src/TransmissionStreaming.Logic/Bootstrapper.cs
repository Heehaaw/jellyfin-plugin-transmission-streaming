namespace TransmissionStreaming.Logic;

using Mediator;
using Microsoft.Extensions.DependencyInjection;

/// <summary>Composes application handlers and other Logic-owned services independently of the host.</summary>
public static class Bootstrapper
{
    /// <summary>Adds the Logic service graph, including its generated Mediator and handlers.</summary>
    public static IServiceCollection AddTransmissionStreamingLogic(this IServiceCollection services)
    {
        return services.AddMediator((MediatorOptions options) =>
            {
                options.ServiceLifetime = ServiceLifetime.Scoped;
                options.GenerateTypesAsInternal = true;
                options.Assemblies = [typeof(Bootstrapper)];
            }
        );
    }
}
