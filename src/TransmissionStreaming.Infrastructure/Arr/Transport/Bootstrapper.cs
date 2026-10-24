namespace TransmissionStreaming.Infrastructure.Arr.Transport;

using Microsoft.Extensions.DependencyInjection;
using System.Net;

/// <summary>Owns the arr HTTP connection policy and transport lifetime.</summary>
internal static class Bootstrapper
{
    private const string ClientName = "TransmissionStreaming.Arr";
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(15);

    public static IServiceCollection AddArrTransport(this IServiceCollection services)
    {
        // API keys are scoped to the configured server; redirects must not forward them.
        services.AddHttpClient<IArrTransport, ArrTransport>(ClientName, client => client.Timeout = RequestTimeout)
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                AllowAutoRedirect = false,
                AutomaticDecompression = DecompressionMethods.None
            });
        return services;
    }
}
