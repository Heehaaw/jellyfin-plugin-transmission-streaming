namespace Jellyfin.Plugin.TransmissionStreaming.ApplicationTests.Catalog;

using global::TransmissionStreaming.TestSupport.Torrents;
using Hosting;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Xunit;

/// <summary>Installs exclusively through Jellyfin's repository API, starting with an empty plugin directory.</summary>
internal sealed class CatalogInstallation(DownloadServices containers) : IAsyncDisposable
{
    private TorrentScenario? scenario;
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
            catalog = new CatalogServer(containers, scenario!, corruptChecksum);
            await catalog.StartAsync(cancellationToken);
        }

        repositoryUrl = repository ?? catalog!.ManifestUrl;
        await StartJellyfinAsync(cancellationToken);
        // Replace defaults so normal tests make no external catalog requests.
        await api!.RequestAsync(
            path: "/Repositories",
            body: new[]
            {
                new
                {
                    Name = source.RepositoryName,
                    Url = repositoryUrl.AbsoluteUri,
                    Enabled = true
                }
            }
        ).WaitAsync(cancellationToken);
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
        scenario = await containers.BeginScenarioAsync(cancellationToken);
        workspace = scenario.Workspace;
        var baseline = await TestContext.Current.GetFixture<JellyfinBaseline>()
                       ?? throw new InvalidOperationException("Register the Jellyfin baseline assembly fixture.");
        baseline.Populate(Path.Combine(workspace, "jellyfin"));
        server = new JellyfinContainer(workspace, containers.Network, installPlugin: false, containers.Workspace);
    }

    private async Task StartJellyfinAsync(CancellationToken cancellationToken)
    {
        await server!.StartAsync(cancellationToken);
        api = new JellyfinApi(server.Address);
        await api.AuthenticateAsync().WaitAsync(cancellationToken);
    }

    public async Task<string> AddEmptyMediaLibrariesAsync(CancellationToken cancellationToken)
    {
        var root = Path.Combine(workspace!, "media");
        foreach (var (name, type, folder) in new[]
                 {
                     ("Movies", "movies", "movies"), ("Shows", "tvshows", "series")
                 })
        {
            var local = Directory.CreateDirectory(Path.Combine(root, folder)).FullName;
            var remote = scenario!.RemotePath(local);
            var query = $"name={name}&collectionType={type}&refreshLibrary=false&paths={Uri.EscapeDataString(remote)}";
            await api!.RequestAsync(
                path: "/Library/VirtualFolders?" + query,
                body: new
                {
                    LibraryOptions = new
                    {
                        EnableRealtimeMonitor = false,
                        EnableInternetProviders = false,
                        SaveLocalMetadata = false
                    }
                }
            ).WaitAsync(cancellationToken);
        }

        return scenario!.RemotePath(root);
    }

    public Task<JsonNode?> PackagesAsync(CancellationToken cancellationToken)
    {
        return api!.RequestAsync("/Packages").WaitAsync(cancellationToken);
    }

    public Task InstallAsync(CancellationToken cancellationToken)
    {
        var packageName = Uri.EscapeDataString(Bundle.Name);
        var repositoryAddress = Uri.EscapeDataString(repositoryUrl!.AbsoluteUri);
        var path = $"/Packages/Installed/{packageName}?assemblyGuid={Bundle.Id}&version={Bundle.Version}"
                   + $"&repositoryUrl={repositoryAddress}";
        return api!.RequestAsync(
            path: path,
            body: new
                { }
        ).WaitAsync(cancellationToken);
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
            if (scenario is not null)
            {
                await scenario.DisposeAsync();
            }
        }
    }
}
