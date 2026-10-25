namespace HdrPlayback.Logic;

using Mediator;
using Microsoft.Extensions.DependencyInjection;
using Playback;

/// <summary>Composes HDR policies with a feature-owned mediator, independently of other installed plugins.</summary>
public static class Bootstrapper
{
    /// <summary>Registers scoped application policies without claiming the host's default mediator interfaces.</summary>
    public static IServiceCollection AddHdrPlaybackLogic(this IServiceCollection services)
    {
        var registrations = new ServiceCollection();
        registrations.AddMediator((MediatorOptions options) =>
        {
            options.Namespace = "HdrPlayback.Logic.Dispatching";
            options.ServiceLifetime = ServiceLifetime.Scoped;
            options.GenerateTypesAsInternal = true;
            options.Assemblies = [typeof(Bootstrapper)];
        }
        );

        // Jellyfin loads plugins into one service collection. HDR must not capture another plugin's unkeyed dispatch contracts.
        foreach (var descriptor in registrations)
        {
            if (descriptor.ServiceType != typeof(IMediator) && descriptor.ServiceType != typeof(ISender) && descriptor.ServiceType != typeof(IPublisher))
            {
                services.Add(descriptor);
            }
        }

        return services.AddKeyedScoped<ISender>(
            serviceKey: typeof(SelectHdrMode),
            implementationFactory: (provider, _) => provider.GetRequiredService<Dispatching.Mediator>()
        );
    }
}
