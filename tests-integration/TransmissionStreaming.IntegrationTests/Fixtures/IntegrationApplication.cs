namespace TransmissionStreaming.IntegrationTests.Fixtures;

using Core.Configuration;
using Core.Projection;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Networks;
using Infrastructure;
using Logic;
using Logic.Discovery;
using Logic.Streaming;
using Mediator;
using Microsoft.Extensions.DependencyInjection;
using TestSupport.Arr;
using TestSupport.Torrents;

/// <summary>Composes the actual application and infrastructure while replacing only Jellyfin's indexing boundary.</summary>
internal sealed class IntegrationApplication : IAsyncDisposable
{
    private INetwork? _network;
    private ServiceProvider? _provider;
    private SettingsProvider? _settings;
    private string? _workspace;
    private TorrentSwarm? _swarm;

    public TorrentSwarm Swarm => _swarm ?? throw new InvalidOperationException("Start the fixture first.");
    public ArrStub Arr { get; private set; } = null!;
    public IMediator Mediator => _provider!.GetRequiredService<IMediator>();
    public IProjectionStore Store => _provider!.GetRequiredService<IProjectionStore>();
    public string Workspace => _workspace!;

    public async Task StartAsync(DownloadSource source, CancellationToken cancellationToken)
    {
        try
        {
            _workspace = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".cache", "transmission-streaming-tests", "integration-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_workspace);
            _network = new NetworkBuilder().Build();
            await _network.CreateAsync(cancellationToken).ConfigureAwait(false);
            _swarm = await TorrentSwarm.StartAsync(_workspace, _network, source.Media, source.FileName, cancellationToken).ConfigureAwait(false);
            Arr = await ArrStub.StartAsync(_workspace, _network, _swarm, cancellationToken).ConfigureAwait(false);
            _settings = new SettingsProvider(new StreamingOptions
            {
                Enabled = true,
                StateDirectory = Path.Combine(_workspace, "state"),
                TransmissionUrl = Swarm.RpcUrl.AbsoluteUri,
                RadarrUrl = Arr.Url.AbsoluteUri,
                RadarrApiKey = Arr.Key,
                SonarrUrl = Arr.Url.AbsoluteUri,
                SonarrApiKey = Arr.Key,
                RemoteMediaRoot = "/data",
                LocalMediaRoot = _workspace,
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

    public void SetEnabled(bool enabled)
    {
        _settings!.Value = _settings.Value with { Enabled = enabled };
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
        await Arr.PublishImportAsync("/data/movies/" + Swarm.FileName, cancellationToken).ConfigureAwait(false);
        return destination;
    }

    public Task<string> DiagnosticsAsync()
    {
        return _swarm is null ? Task.FromResult("The disposable swarm did not finish starting.") : _swarm.LogsAsync(CancellationToken.None);
    }

    public async ValueTask DisposeAsync()
    {
        // Each owner gets its cleanup even if an earlier disposal fails.
        try
        {
            if (_provider is not null)
            {
                await _provider.DisposeAsync().ConfigureAwait(false);
            }
        }
        finally
        {
            try
            {
                if (_swarm is not null)
                {
                    await _swarm.DisposeAsync().ConfigureAwait(false);
                }
            }
            finally
            {
                try
                {
                    if (_network is not null)
                    {
                        await _network.DisposeAsync().ConfigureAwait(false);
                    }
                }
                finally
                {
                    if (_workspace is not null && Directory.Exists(_workspace))
                    {
                        Directory.Delete(_workspace, true);
                    }
                }
            }
        }
    }

    private ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IStreamingOptionsProvider>(_settings!);
        services.AddSingleton<IProjectionLibrary, NoopLibrary>();
        services.AddTransmissionStreamingInfrastructure().AddTransmissionStreamingLogic();
        services.AddMediator((MediatorOptions options) =>
        {
            options.Namespace = "TransmissionStreaming.IntegrationTests.Dispatch";
            options.ServiceLifetime = ServiceLifetime.Singleton;
            options.GenerateTypesAsInternal = true;
            options.Assemblies = [typeof(DiscoverDownloads.Command)];
        });
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
    }

    private sealed class SettingsProvider(StreamingOptions value) : IStreamingOptionsProvider
    {
        public StreamingOptions Value { get; set; } = value;
        public StreamingOptions Get()
        {
            return Value;
        }
    }

    private sealed class NoopLibrary : IProjectionLibrary
    {
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
