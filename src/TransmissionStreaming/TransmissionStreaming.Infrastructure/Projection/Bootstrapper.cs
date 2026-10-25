namespace TransmissionStreaming.Infrastructure.Projection;

using Core.Projection;
using Microsoft.Extensions.DependencyInjection;

/// <summary>Owns the projection feature's implementation lifetimes.</summary>
internal static class Bootstrapper
{
    public static IServiceCollection AddProjectionFiles(this IServiceCollection services)
    {
        services.AddSingleton<IProjectionFiles, ProjectionFiles>();
        return services;
    }
}
