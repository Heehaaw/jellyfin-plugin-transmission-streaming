namespace TransmissionStreaming.Logic;

using Microsoft.Extensions.DependencyInjection;
using TransmissionStreaming.Logic.Configuration;
using TransmissionStreaming.Logic.Discovery;

/// <summary>Composes host-independent feature services into the outer application's container.</summary>
public static class Bootstrapper
{
    /// <summary>Adds host-independent feature services used by the generated application handlers.</summary>
    public static IServiceCollection AddTransmissionStreamingLogic(this IServiceCollection services)
    {
        return services
            .AddConfigurationLogic()
            .AddDiscoveryLogic();
    }
}
