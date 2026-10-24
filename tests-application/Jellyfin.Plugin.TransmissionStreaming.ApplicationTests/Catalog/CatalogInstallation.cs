namespace Jellyfin.Plugin.TransmissionStreaming.ApplicationTests.Catalog;

using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Networks;
using Hosting;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

/// <summary>Installs exclusively through Jellyfin's repository API, starting with an empty plugin directory.</summary>
internal sealed class CatalogInstallation : IAsyncDisposable
{
    private INetwork? network;
    private CatalogServer? catalog;
    private JellyfinContainer? server;
    private JellyfinApi? api;
    private string? workspace;
    private Uri? repositoryUrl;

    public PluginBundle Bundle => PluginBundle.BuildOutput;
    public string InstalledDirectory => Path.Combine(workspace!, "jellyfin", "plugins", Bundle.FolderName);
    public JellyfinApi Api => api!;
    public string? DebugOutputDirectory { get; private set; }

    public async Task StartAsync(CatalogSource source, CancellationToken cancellationToken, bool corruptChecksum = false, Uri? repository = null)
    {
        if (repository is not null && (repository.Scheme != Uri.UriSchemeHttps || !string.IsNullOrEmpty(repository.UserInfo) || !string.IsNullOrEmpty(repository.Query)))
        {
            throw new ArgumentException("An external test repository must use HTTPS without credentials or query parameters.");
        }
        if (corruptChecksum && repository is not null)
        {
            throw new ArgumentException("The checksum rejection scenario requires the controlled local catalog.");
        }
        await PrepareAsync(source, cancellationToken);
        if (repository is null)
        {
            catalog = new CatalogServer(workspace!, network!, corruptChecksum);
            await catalog.StartAsync(cancellationToken);
        }
        repositoryUrl = repository ?? catalog!.ManifestUrl;
        await StartJellyfinAsync(cancellationToken);
        // Replace defaults so normal tests make no external catalog requests.
        await api!.RequestAsync("/Repositories", new[]
        {
            new { Name = source.RepositoryName, Url = repositoryUrl.AbsoluteUri, Enabled = true }
        }).WaitAsync(cancellationToken);
    }

    public async Task StartDevelopmentAsync(CatalogSource source, CancellationToken cancellationToken)
    {
        await PrepareAsync(source, cancellationToken);
        var pluginsDirectory = Path.Combine(workspace!, "jellyfin", "plugins");
        DebugOutputDirectory = await DevelopmentInstall.RunAsync(pluginsDirectory, cancellationToken);
        await StartJellyfinAsync(cancellationToken);
    }

    private async Task PrepareAsync(CatalogSource source, CancellationToken cancellationToken)
    {
        workspace = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".cache", "transmission-streaming-tests", "installation-" + source.RunId.ToString("N"));
        Directory.CreateDirectory(workspace);
        network = new NetworkBuilder().Build();
        await network.CreateAsync(cancellationToken);
        server = new JellyfinContainer(workspace, network, installPlugin: false);
    }

    private async Task StartJellyfinAsync(CancellationToken cancellationToken)
    {
        await server!.StartAsync(cancellationToken);
        api = new JellyfinApi(server.Address);
        await api.AuthenticateAsync().WaitAsync(cancellationToken);
    }

    public Task<JsonNode?> PackagesAsync(CancellationToken cancellationToken)
    {
        return api!.RequestAsync("/Packages").WaitAsync(cancellationToken);
    }

    public Task InstallAsync(CancellationToken cancellationToken)
    {
        var path = $"/Packages/Installed/{Uri.EscapeDataString(Bundle.Name)}?assemblyGuid={Bundle.Id}&version={Bundle.Version}&repositoryUrl={Uri.EscapeDataString(repositoryUrl!.AbsoluteUri)}";
        return api!.RequestAsync(path, new { }).WaitAsync(cancellationToken);
    }

    public async Task RestartAsync(CancellationToken cancellationToken)
    {
        await server!.RestartAsync(cancellationToken);
        api!.Dispose();
        api = new JellyfinApi(server.Address);
        await api.AuthenticateAsync().WaitAsync(cancellationToken);
    }

    public async Task<string> DiagnosticsAsync()
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        try
        {
            var serverLogs = server is null ? "Jellyfin was not created." : await server.LogsAsync().WaitAsync(deadline.Token);
            var catalogLogs = catalog is null ? "Catalog was not created." : await catalog.LogsAsync(deadline.Token);
            var tail = string.Join('\n', (serverLogs + catalogLogs).Split('\n').TakeLast(100));
            return Regex.Replace(tail, "(?i)(key=)[^&\\s\\\"<>]+", "$1[redacted]");
        }
        catch (Exception exception)
        {
            return $"Catalog test logs unavailable ({exception.GetType().Name}).";
        }
    }

    public async ValueTask DisposeAsync()
    {
        api?.Dispose();
        try
        {
            if (server is not null)
            {
                await server.DisposeAsync();
            }
        }
        finally
        {
            try
            {
                if (catalog is not null)
                {
                    await catalog.DisposeAsync();
                }
            }
            finally
            {
                try
                {
                    if (network is not null)
                    {
                        await network.DisposeAsync();
                    }
                }
                finally
                {
                    if (workspace is not null && Directory.Exists(workspace))
                    {
                        Directory.Delete(workspace, true);
                    }
                }
            }
        }
    }
}
