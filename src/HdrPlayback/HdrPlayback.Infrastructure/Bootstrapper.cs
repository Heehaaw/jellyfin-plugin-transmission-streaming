namespace HdrPlayback.Infrastructure;

using Core.Playback;
using Core.Transcoding;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Playback;
using Transcoding;

/// <summary>Registers HDR command adaptation and transient native-session selection.</summary>
public static class Bootstrapper
{
    /// <summary>Adds host-neutral infrastructure without changing Jellyfin's encoder configuration.</summary>
    public static IServiceCollection AddHdrPlaybackInfrastructure(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        return services.AddSingleton<IHdrSessionStore, HdrSessionStore>().AddSingleton<IHdrCommandTransformer, HdrCommandTransformer>();
    }
}
