namespace TransmissionStreaming.Logic;

using Microsoft.Extensions.DependencyInjection;
using TransmissionStreaming.Logic.Discovery;

/// <summary>Composes host-independent feature services into the outer application's container.</summary>
public static class Bootstrapper
{
    public static IServiceCollection AddTransmissionStreamingLogic(this IServiceCollection services)
    {
        return services.AddDiscoveryLogic();
    }
}
