namespace TransmissionStreaming.Core.Streaming;

using Microsoft.Extensions.DependencyInjection;

/// <summary>Shares the stateless copy engine; each request owns its source and rented buffer.</summary>
internal static class Bootstrapper
{
    public static void Register(IServiceCollection services)
    {
        services.AddSingleton<ProgressiveReader>();
    }
}
