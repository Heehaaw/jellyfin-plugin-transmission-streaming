namespace TransmissionStreaming.Core.Transmission;

using System.Net;
using Microsoft.Extensions.DependencyInjection;

/// <summary>Owns the Transmission RPC transport and its redirect policy.</summary>
internal static class Bootstrapper
{
    private const string ClientName = "TransmissionStreaming.Transmission";
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(15);

    public static void Register(IServiceCollection services)
    {
        services.AddHttpClient(ClientName, client => client.Timeout = RequestTimeout)
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                AllowAutoRedirect = false,
                AutomaticDecompression = DecompressionMethods.None
            });
        services.AddSingleton(provider => new TransmissionClient(provider.GetRequiredService<IHttpClientFactory>().CreateClient(ClientName)));
    }
}
