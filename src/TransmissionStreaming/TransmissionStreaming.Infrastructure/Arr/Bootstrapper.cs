namespace TransmissionStreaming.Infrastructure.Arr;

using Core.Arr;
using Microsoft.Extensions.DependencyInjection;
using Transport;

/// <summary>Composes the read-only arr protocol adapter.</summary>
internal static class Bootstrapper
{
    public static IServiceCollection AddArrIntegration(this IServiceCollection services)
    {
        services.AddArrTransport();
        return services.AddSingleton<IArrClient, ArrClient>();
    }
}
