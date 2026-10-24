namespace Jellyfin.Plugin.TransmissionStreaming.Persistence;

using Microsoft.Extensions.DependencyInjection;

/// <summary>Registers durable stream identities separately from the Jellyfin media database.</summary>
internal static class Bootstrapper
{
    public static void Register(IServiceCollection services)
    {
        services.AddSingleton<ProjectionStore>();
    }
}
