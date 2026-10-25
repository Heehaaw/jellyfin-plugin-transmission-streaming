namespace TransmissionStreaming.IntegrationTests.Fixtures;

using Core.Configuration;
using Core.Projection;
using Core.Transmission;
using Infrastructure;
using Logic;
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

    public ArrStub Arr
    {
        get;
        private set;
    } = null!;

    public IProjectionStore Store => _provider!.GetRequiredService<IProjectionStore>();
    public string Workspace => _workspace!;

    public async Task StartAsync(DownloadSource source, CancellationToken cancellationToken)
    {
        try
        {
            _swarm = await containers.OpenAsync(source.Media, source.FileName, cancellationToken).ConfigureAwait(false);
            _workspace = _swarm.Workspace;
            Arr = await ArrStub.StartAsync(_workspace, containers.Network, _swarm, cancellationToken).ConfigureAwait(false);
            var settings = new StreamingOptions
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
            };
            _settings = new MutableOptionsMonitor(settings);
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

    public async Task DiscoverAsync(DiscoverDownloads.Command command, CancellationToken cancellationToken)
    {
        await using var scope = _provider!.CreateAsyncScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        await mediator.Send(command, cancellationToken).ConfigureAwait(false);
    }

    public Task<IReadOnlyList<TorrentSnapshot>> TorrentsAsync(CancellationToken cancellationToken)
    {
        var settings = _settings!.CurrentValue;
        var connection = new TransmissionConnection(settings.TransmissionUrl, settings.TransmissionUsername, settings.TransmissionPassword);
        var client = _provider!.GetRequiredService<ITransmissionClient>();
        return client.GetTorrentsAsync(connection, cancellationToken);
    }

    public void UseIndependentArrMounts()
    {
        _independentArrMounts = true;
        var localArrRoot = Path.Combine(_workspace!, "movies");
        _settings!.CurrentValue = _settings.CurrentValue with
        {
            RemoteMediaRoot = "/unused-legacy",
            LocalMediaRoot = Path.Combine(_workspace!, "unused-legacy"),
            PathMappings =
            [
                new()
                {
                    Service = MediaService.Transmission,
                    RemoteRoot = "/data",
                    LocalRoot = containers.Workspace
                },
                new()
                {
                    Service = MediaService.Radarr,
                    RemoteRoot = "/arr/library",
                    LocalRoot = localArrRoot
                },
                new()
                {
                    Service = MediaService.Sonarr,
                    RemoteRoot = "/arr/library",
                    LocalRoot = localArrRoot
                }
            ]
        };
    }

    public void SetNativeHandoffReady(string path, LibraryItem result)
    {
        var host = (NoopLibrary)_provider!.GetRequiredService<IProjectionLibrary>();
        host.NativePath = path;
        host.NativeItem = result;
    }

    public void SetEnabled(bool enabled)
    {
        _settings!.CurrentValue = _settings.CurrentValue with
        {
            Enabled = enabled
        };
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
        await using var scope = _provider!.CreateAsyncScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var command = new StreamMedia.Command(entry, destination, offset, count);
        await mediator.Send(command, cancellationToken).ConfigureAwait(false);
        return destination.ToArray();
    }

    public async Task<string> ImportAsync(CancellationToken cancellationToken)
    {
        var destination = Path.Combine(_workspace!, "movies", Swarm.FileName);
        File.Move(Swarm.LocalDownloadPath, destination);
        var remotePath = _independentArrMounts ? "/arr/library/" + Swarm.FileName : Swarm.RemotePath(destination);
        await Arr.PublishImportAsync(remotePath, cancellationToken).ConfigureAwait(false);
        return destination;
    }

    public Task<string> DiagnosticsAsync()
    {
        return _swarm is null
            ? Task.FromResult("The disposable swarm did not finish starting.")
            : _swarm.LogsAsync(CancellationToken.None);
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
        services.AddSingleton<IProjectionOverview, NoopOverview>();
        services.AddSingleton<IMediaLibraryLocations, NoopLibrary>();
        services.AddTransmissionStreamingInfrastructure();
        services.AddTransmissionStreamingLogic();
        return services.BuildServiceProvider(
            new ServiceProviderOptions
            {
                ValidateOnBuild = true,
                ValidateScopes = true
            }
        );
    }

    /// <summary>The fixture changes current settings directly; production notifications use the framework monitor.</summary>
    private sealed class MutableOptionsMonitor(StreamingOptions value) : IOptionsMonitor<StreamingOptions>
    {
        public StreamingOptions CurrentValue
        {
            get;
            set;
        } = value;

        public StreamingOptions Get(string? name)
        {
            return CurrentValue;
        }

        public IDisposable? OnChange(Action<StreamingOptions, string?> listener)
        {
            return null;
        }
    }

    private sealed class NoopOverview : IProjectionOverview
    {
        public string? Format(string? overview, string? publicUrl)
        {
            return overview;
        }
    }

    private sealed class NoopLibrary : IProjectionLibrary, IMediaLibraryLocations
    {
        public string? NativePath
        {
            get;
            set;
        }

        public LibraryItem? NativeItem
        {
            get;
            set;
        }

        public LibraryItem? Find(string path)
        {
            return path == NativePath ? NativeItem : null;
        }

        public Task IndexImportedAsync(string path, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task UpdateMetadataAsync(string itemId, string name, string? overview, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public IReadOnlyList<LibraryUserState> GetUserStates(string sourceItemId, string targetItemId)
        {
            return [];
        }

        public void SaveUserState(string userId, string itemId, LibraryWatchState state)
        {
        }

        public void Remove(string itemId)
        {
        }

        public IReadOnlyList<string> GetServicePaths(MediaService service)
        {
            return [];
        }

        public IReadOnlyList<string> GetAllPaths()
        {
            return [];
        }

        public IReadOnlyList<string> GetPaths()
        {
            return [];
        }

        public Task RefreshAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
