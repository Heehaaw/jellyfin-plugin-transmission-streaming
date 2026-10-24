namespace TransmissionStreaming.Arr.Transport;

using System.Net;
using Microsoft.Extensions.DependencyInjection;

/// <summary>Owns the arr HTTP connection policy and transport lifetime.</summary>
internal static class Bootstrapper
{
    private const string ClientName = "TransmissionStreaming.Arr";
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(15);

    public static void Register(IServiceCollection services)
    {
        // API keys are scoped to the configured server; redirects must not forward them.
        services.AddHttpClient(ClientName, client => client.Timeout = RequestTimeout)
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                AllowAutoRedirect = false,
                AutomaticDecompression = DecompressionMethods.None
            });
        services.AddSingleton(provider => new ArrTransport(provider.GetRequiredService<IHttpClientFactory>().CreateClient(ClientName)));
    }
}
