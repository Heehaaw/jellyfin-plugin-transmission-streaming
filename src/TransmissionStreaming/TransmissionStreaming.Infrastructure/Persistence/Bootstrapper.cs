namespace TransmissionStreaming.Infrastructure.Persistence;

using Core.Projection;
using Microsoft.Extensions.DependencyInjection;

/// <summary>Owns the persistence feature's implementation lifetimes.</summary>
internal static class Bootstrapper
{
    public static IServiceCollection AddProjectionPersistence(this IServiceCollection services)
    {
        services.AddSingleton<IProjectionStore, ProjectionStore>();
        return services;
    }
}
