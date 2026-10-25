namespace TransmissionStreaming.IntegrationTests.Discovery;

using Fixtures;
using FluentAssertions;
using Xunit;

public sealed class DiscoveryEligibilityTests(ITestOutputHelper output)
{
    [Theory, AutoMoqData]
    internal async Task Should_NotCreateLibraryEntriesForSamples(DownloadSource source, IntegrationApplication sut)
    {
        // Arrange
        await using var application = sut;
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        deadline.CancelAfter(TimeSpan.FromMinutes(2));
        var cancellationToken = deadline.Token;
        var sample = source with
        {
            FileName = Path.GetFileNameWithoutExtension(source.FileName) + ".sample.mkv"
        };
        await sut.StartAsync(sample, cancellationToken);

        try
        {
            // Act
            await sut.DiscoverAsync(source.Discover, cancellationToken);
            var torrents = await sut.TorrentsAsync(cancellationToken);
            var entries = sut.Store.All();
            var streamFiles = Directory.EnumerateFiles(Path.Combine(sut.Workspace, "projections"), "*.strm", SearchOption.AllDirectories).ToArray();

            // Assert
            torrents.Should().ContainSingle(torrent => torrent.Hash == sut.Swarm.Hash);
            entries.Should().BeEmpty();
            streamFiles.Should().BeEmpty();
        }
        catch
        {
            output.WriteLine(await sut.DiagnosticsAsync());
            throw;
        }
    }

    [Theory, AutoMoqData]
    internal async Task Should_NotAssignAnAmbiguousMultiEpisodeFileToOneEpisode(DownloadSource source, IntegrationApplication sut)
    {
        // Arrange
        await using var application = sut;
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        deadline.CancelAfter(TimeSpan.FromMinutes(2));
        var cancellationToken = deadline.Token;
        var ambiguous = source with
        {
            FileName = Path.GetFileNameWithoutExtension(source.FileName) + ".S01E02E03.mkv"
        };
        await sut.StartAsync(ambiguous, cancellationToken);
        await sut.Arr.SetEpisodeAsync(true, cancellationToken);

        try
        {
            // Act
            await sut.DiscoverAsync(source.Discover, cancellationToken);
            var torrents = await sut.TorrentsAsync(cancellationToken);
            var entries = sut.Store.All();

            // Assert
            torrents.Should().ContainSingle(torrent => torrent.Hash == sut.Swarm.Hash);
            entries.Should().BeEmpty("the queued series alone cannot resolve which episode owns this file");
        }
        catch
        {
            output.WriteLine(await sut.DiagnosticsAsync());
            throw;
        }
    }
}
