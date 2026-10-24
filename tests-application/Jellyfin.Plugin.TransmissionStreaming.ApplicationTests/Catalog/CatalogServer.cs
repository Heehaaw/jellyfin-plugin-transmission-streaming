namespace Jellyfin.Plugin.TransmissionStreaming.ApplicationTests.Catalog;

using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Networks;
using global::TransmissionStreaming.TestSupport;

/// <summary>Serves a local repository on the same isolated network as the disposable Jellyfin.</summary>
internal sealed class CatalogServer : IAsyncDisposable
{
    private const string Image = "mcr.microsoft.com/dotnet/sdk:10.0.401@sha256:e70cdb7f80b0348f5cb85f19a8f670fca061f033d57eed12fa003d58b0e06317";
    private const string NetworkAlias = "catalog";
    private const int HttpPort = 8080;
    private const string EntryPath = "/CatalogHttp.cs";
    private readonly IContainer container;

    public CatalogServer(string workspace, INetwork network, bool corruptChecksum)
    {
        var root = Path.Combine(workspace, "catalog");
        var packageUrl = new UriBuilder("http", NetworkAlias, HttpPort, "/package.zip").Uri;
        CatalogPackage.Write(root, packageUrl, corruptChecksum);
        using var script = typeof(CatalogServer).Assembly.GetManifestResourceStream(
            "Jellyfin.Plugin.TransmissionStreaming.ApplicationTests.Catalog.Scripts.CatalogHttp.cs")!;
        using var content = new MemoryStream();
        script.CopyTo(content);
        container = new ContainerBuilder(Image)
            .WithCreateParameterModifier(parameters => parameters.User = ContainerIdentity.User)
            .WithNetwork(network)
            .WithNetworkAliases(NetworkAlias)
            .WithPortBinding(HttpPort, true)
            .WithBindMount(root, "/catalog")
            .WithResourceMapping(content.ToArray(), EntryPath)
            .WithEnvironment("CATALOG_ROOT", "/catalog")
            .WithEnvironment("ASPNETCORE_URLS", $"http://0.0.0.0:{HttpPort}")
            .WithEnvironment("DOTNET_CLI_TELEMETRY_OPTOUT", "1")
            .WithEnvironment("DOTNET_NOLOGO", "1")
            .WithEnvironment("DOTNET_CLI_HOME", "/tmp")
            .WithEnvironment("NUGET_PACKAGES", "/tmp/nuget")
            .WithEnvironment("XDG_DATA_HOME", "/tmp/data")
            .WithEnvironment("XDG_CACHE_HOME", "/tmp/cache")
            .WithCommand("dotnet", "run", "--file", EntryPath)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(request => request.ForPort(HttpPort).ForPath("/health")))
            .Build();
    }

    public Uri ManifestUrl => new UriBuilder("http", NetworkAlias, HttpPort, "/manifest.json").Uri;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        return container.StartAsync(cancellationToken);
    }

    public async Task<string> LogsAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(container.Id))
        {
            return "Catalog container was not created.";
        }
        var logs = await container.GetLogsAsync(ct: cancellationToken);
        return logs.Stdout + logs.Stderr;
    }

    public ValueTask DisposeAsync()
    {
        return container.DisposeAsync();
    }
}
