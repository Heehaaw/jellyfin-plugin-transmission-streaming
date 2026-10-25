namespace TransmissionStreaming.Infrastructure.Filesystem;

using Core.Filesystem;
using Microsoft.Extensions.DependencyInjection;

/// <summary>Owns the filesystem feature's implementation lifetimes.</summary>
internal static class Bootstrapper
{
    public static IServiceCollection AddMediaFilesystem(this IServiceCollection services)
    {
        services.AddSingleton<IMediaPathMapper, PhysicalMediaPathMapper>();
        services.AddSingleton<IMediaFiles, PhysicalMediaFiles>();
        return services;
    }
}
