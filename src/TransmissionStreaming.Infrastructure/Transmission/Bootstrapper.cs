namespace TransmissionStreaming.Infrastructure.Transmission;

using Core.Transmission;
using Microsoft.Extensions.DependencyInjection;
using System.Net;

/// <summary>Owns the Transmission RPC transport and its redirect policy.</summary>
internal static class Bootstrapper
{
    private const string ClientName = "TransmissionStreaming.Transmission";
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(15);

    public static IServiceCollection AddTransmissionTransport(this IServiceCollection services)
    {
        services.AddHttpClient<ITransmissionClient, TransmissionClient>(ClientName, client => client.Timeout = RequestTimeout)
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
                {
                    AllowAutoRedirect = false,
                    AutomaticDecompression = DecompressionMethods.None
                }
            );
        return services;
    }
}
