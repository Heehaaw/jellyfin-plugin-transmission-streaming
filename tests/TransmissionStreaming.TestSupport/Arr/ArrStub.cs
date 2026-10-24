namespace TransmissionStreaming.TestSupport.Arr;

using DotNet.Testcontainers.Networks;
using Torrents;

/// <summary>Controls the arr responses served alongside the local seed, without running a real indexer or download manager.</summary>
internal sealed class ArrStub(TorrentScenario swarm)
{
    public string Key => SwarmSettings.ArrKey;
    public Uri Url => swarm.ControlUrl;
    public Uri ContainerUrl => SwarmSettings.ContainerArrUrl;
    public string Title => swarm.Title;
    public const int MovieId = 1;

    public static Task<ArrStub> StartAsync(string workspace, INetwork network, TorrentScenario swarm, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new ArrStub(swarm));
    }

    public Task PublishImportAsync(string remotePath, CancellationToken cancellationToken)
    {
        return swarm.ControlAsync("import", new { path = remotePath }, cancellationToken);
    }

    public Task PublishUnrelatedImportAsync(string remotePath, string differentHash, CancellationToken cancellationToken)
    {
        return swarm.ControlAsync("import", new { path = remotePath, hash = differentHash }, cancellationToken);
    }

    public Task SetEpisodeAsync(bool enabled, CancellationToken cancellationToken)
    {
        return swarm.ControlAsync("episode", new { enabled }, cancellationToken);
    }

    public Task SetFailureAsync(bool enabled, CancellationToken cancellationToken)
    {
        return swarm.ControlAsync("failure", new { enabled }, cancellationToken);
    }
}
