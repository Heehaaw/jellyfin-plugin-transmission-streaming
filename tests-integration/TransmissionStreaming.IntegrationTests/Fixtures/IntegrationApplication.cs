namespace TransmissionStreaming.IntegrationTests.Fixtures;

using Core.Configuration;
using Core.Projection;
using Core.Transmission;
using Infrastructure;
using Logic.Discovery;
using Logic.Streaming;
using Mediator;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using TestSupport.Arr;
using TestSupport.Torrents;

/// <summary>Composes the actual application and infrastructure while replacing only Jellyfin's indexing boundary.</summary>
internal sealed class IntegrationApplication(DownloadServices containers) : IAsyncDisposable
{
    private ServiceProvider? _provider;
    private MutableOptionsMonitor? _settings;
    private string? _workspace;
    private TorrentScenario? _swarm;
    private bool _independentArrMounts;

    public TorrentScenario Swarm => _swarm ?? throw new InvalidOperationException("Start the fixture first.");
    public ArrStub Arr { get; private set; } = null!;
    public IMediator Mediator => _provider!.GetRequiredService<IMediator>();
    public IProjectionStore Store => _provider!.GetRequiredService<IProjectionStore>();
    public string Workspace => _workspace!;

    public async Task StartAsync(DownloadSource source, CancellationToken cancellationToken)
    {
        try
        {
            _swarm = await containers.OpenAsync(source.Media, source.FileName, cancellationToken).ConfigureAwait(false);
            _workspace = _swarm.Workspace;
            Arr = await ArrStub.StartAsync(_workspace, containers.Network, _swarm, cancellationToken).ConfigureAwait(false);
            _settings = new MutableOptionsMonitor(new StreamingOptions
            {
                Enabled = true,
                StateDirectory = Path.Combine(_workspace, "state"),
                TransmissionUrl = Swarm.RpcUrl.AbsoluteUri,
                RadarrUrl = Arr.Url.AbsoluteUri,
                RadarrApiKey = Arr.Key,
                SonarrUrl = Arr.Url.AbsoluteUri,
                SonarrApiKey = Arr.Key,
                RemoteMediaRoot = "/data",
                LocalMediaRoot = containers.Workspace,
                ProjectionRoot = Path.Combine(_workspace, "projections"),
                JellyfinPublicUrl = "http://unused-jellyfin.invalid",
                PrioritizePlayback = true,
                PieceWaitSeconds = 5
            });
            _provider = BuildProvider();
        }
        catch
        {
            if (_swarm is not null)
            {
                Console.Error.WriteLine(await DiagnosticsAsync());
            }

            throw;
        }
    }

    public Task<IReadOnlyList<TorrentSnapshot>> TorrentsAsync(CancellationToken cancellationToken)
    {
        var settings = _settings!.CurrentValue;
        var connection = new TransmissionConnection(settings.TransmissionUrl, settings.TransmissionUsername, settings.TransmissionPassword);
        return _provider!.GetRequiredService<ITransmissionClient>().GetTorrentsAsync(connection, cancellationToken);
    }

    public void UseIndependentArrMounts()
    {
        _independentArrMounts = true;
        _settings!.CurrentValue = _settings.CurrentValue with
        {
            RemoteMediaRoot = "/unused-legacy",
            LocalMediaRoot = Path.Combine(_workspace!, "unused-legacy"),
            PathMappings =
            [
                new() { Service = MediaService.Transmission, RemoteRoot = "/data", LocalRoot = containers.Workspace },
                new() { Service = MediaService.Radarr, RemoteRoot = "/arr/library", LocalRoot = Path.Combine(_workspace!, "movies") },
                new() { Service = MediaService.Sonarr, RemoteRoot = "/arr/library", LocalRoot = Path.Combine(_workspace!, "movies") }
            ]
        };
    }

    public void SetEnabled(bool enabled)
    {
        _settings!.CurrentValue = _settings.CurrentValue with { Enabled = enabled };
    }

    public async Task RestartAsync()
    {
        // The second service provider must reconstruct identities from SQLite,
        // not retain an in-memory reference from the preceding application lifetime.
        await _provider!.DisposeAsync().ConfigureAwait(false);
        _provider = BuildProvider();
    }

    public async Task<byte[]> ReadAsync(Projection entry, long offset, long count, CancellationToken cancellationToken)
    {
        await using var destination = new MemoryStream();
        await Mediator.Send(new StreamMedia.Command(entry, destination, offset, count), cancellationToken).ConfigureAwait(false);
        return destination.ToArray();
    }

    public async Task<string> ImportAsync(CancellationToken cancellationToken)
    {
        var destination = Path.Combine(_workspace!, "movies", Swarm.FileName);
        File.Move(Swarm.LocalDownloadPath, destination);
        await Arr.PublishImportAsync(_independentArrMounts ? "/arr/library/" + Swarm.FileName : Swarm.RemotePath(destination), cancellationToken).ConfigureAwait(false);
        return destination;
    }

    public Task<string> DiagnosticsAsync()
    {
        return _swarm is null ? Task.FromResult("The disposable swarm did not finish starting.") : _swarm.LogsAsync(CancellationToken.None);
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (_provider is not null)
            {
                await _provider.DisposeAsync().ConfigureAwait(false);
            }
        }
        finally
        {
            if (_swarm is not null)
            {
                await _swarm.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    private ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IOptionsMonitor<StreamingOptions>>(_settings!);
        services.AddSingleton<IProjectionLibrary, NoopLibrary>();
        services.AddSingleton<IMediaLibraryLocations, NoopLibrary>();
        services.AddTransmissionStreamingInfrastructure();
        services.AddMediator((MediatorOptions options) =>
        {
            options.Namespace = "TransmissionStreaming.IntegrationTests.Dispatch";
            options.ServiceLifetime = ServiceLifetime.Singleton;
            options.GenerateTypesAsInternal = true;
            options.Assemblies = [typeof(DiscoverDownloads.Command)];
        });
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
    }

    /// <summary>The fixture changes current settings directly; production notifications use the framework monitor.</summary>
    private sealed class MutableOptionsMonitor(StreamingOptions value) : IOptionsMonitor<StreamingOptions>
    {
        public StreamingOptions CurrentValue { get; set; } = value;

        public StreamingOptions Get(string? name) => CurrentValue;

        public IDisposable? OnChange(Action<StreamingOptions, string?> listener) => null;
    }

    private sealed class NoopLibrary : IProjectionLibrary, IMediaLibraryLocations
    {
        public IReadOnlyList<string> GetServicePaths(MediaService service) => [];

        public IReadOnlyList<string> GetAllPaths() => [];

        public IReadOnlyList<string> GetPaths()
        {
            return [];
        }

        public Task<bool> TryRemoveAsync(Projection entry, CancellationToken cancellationToken) => Task.FromResult(true);

        public Task RefreshAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task ReconcileAsync(Projection entry, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
