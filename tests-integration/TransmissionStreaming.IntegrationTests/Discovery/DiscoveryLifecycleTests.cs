namespace TransmissionStreaming.IntegrationTests.Discovery;

using Fixtures;
using FluentAssertions;
using Xunit;

/// <summary>Exercises release ownership and the download-to-import states across real transports and persisted application lifetimes.</summary>
public sealed class DiscoveryLifecycleTests(ITestOutputHelper output)
{
    [Theory, AutoMoqData]
    internal async Task Should_KeepTheExistingReleaseWhenAnotherTorrentTargetsTheSameMovie(DownloadSource source, DownloadSource replacement, IntegrationApplication sut)
    {
        // Arrange
        await using var application = sut;
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        deadline.CancelAfter(TimeSpan.FromMinutes(2));
        var cancellationToken = deadline.Token;
        await sut.StartAsync(source, cancellationToken);
        await sut.DiscoverAsync(source.Discover, cancellationToken);
        var original = sut.Store.All().Single();
        var originalLink = await File.ReadAllTextAsync(original.StreamPath, cancellationToken);
        var upgrade = replacement with
        {
            FileName = Path.GetFileNameWithoutExtension(replacement.FileName) + ".2160p.mkv"
        };

        try
        {
            // Act
            // Both torrents stay in the actual daemon, while arr now owns the newer release for the same movie ID.
            await sut.Swarm.AddAsync(upgrade.Media, upgrade.FileName, cancellationToken);
            var replacementHash = sut.Swarm.Hash;
            await sut.DiscoverAsync(source.Discover, cancellationToken);
            await sut.RestartAsync();
            await sut.DiscoverAsync(source.Discover, cancellationToken);
            var retained = sut.Store.All();
            var torrents = await sut.TorrentsAsync(cancellationToken);
            var currentLink = await File.ReadAllTextAsync(original.StreamPath, cancellationToken);

            // Assert
            torrents.Select(torrent => torrent.Hash).Should().BeEquivalentTo([original.TorrentHash, replacementHash]);
            retained.Should().ContainSingle();
            retained[0].Should().BeEquivalentTo(original, options => options.Excluding(projection => projection.Secret));
            string.Equals(retained[0].Secret, original.Secret, StringComparison.Ordinal).Should().BeTrue("a new release must not take over the saved capability");
            string.Equals(currentLink, originalLink, StringComparison.Ordinal).Should().BeTrue("the projected library entry remains owned by the original release");
        }
        catch
        {
            output.WriteLine(await sut.DiagnosticsAsync());
            throw;
        }
    }

    [Theory, AutoMoqData]
    internal async Task Should_RecoverALateImportAfterTheTorrentHasAlreadyBeenRemoved(DownloadSource source, IntegrationApplication sut)
    {
        // Arrange
        await using var application = sut;
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        deadline.CancelAfter(TimeSpan.FromMinutes(2));
        var cancellationToken = deadline.Token;
        await sut.StartAsync(source, cancellationToken, completed: true);
        await sut.DiscoverAsync(source.Discover, cancellationToken);
        var original = sut.Store.All().Single();
        var unavailableStatus = "Download unavailable";
        var completeStatus = "Waiting for Jellyfin handoff";
        var completeProgress = 1d;

        try
        {
            // Act
            await sut.Swarm.RemoveAsync(cancellationToken);
            await sut.DiscoverAsync(source.Discover, cancellationToken);
            var unavailable = sut.Store.All().Single();
            var importedPath = await sut.ImportAsync(cancellationToken);
            await sut.RestartAsync();
            await sut.DiscoverAsync(source.Discover, cancellationToken);
            var recovered = sut.Store.All().Single();
            var bytes = await sut.ReadAsync(recovered, 0, source.Media.Length, cancellationToken);
            var torrents = await sut.TorrentsAsync(cancellationToken);

            // Assert
            unavailable.Status.Should().Be(unavailableStatus);
            unavailable.CompletedPath.Should().BeNull();
            unavailable.Id.Should().Be(original.Id);
            recovered.Status.Should().Be(completeStatus);
            recovered.Progress.Should().Be(completeProgress);
            recovered.Id.Should().Be(original.Id);
            recovered.StreamPath.Should().Be(original.StreamPath);
            recovered.CompletedPath.Should().Be(importedPath);
            string.Equals(recovered.Secret, original.Secret, StringComparison.Ordinal).Should().BeTrue("late import resolves the existing entry instead of replacing it");
            torrents.Should().BeEmpty();
            bytes.Should().Equal(source.Media);
        }
        catch
        {
            output.WriteLine(await sut.DiagnosticsAsync());
            throw;
        }
    }

    [Theory, AutoMoqData]
    internal async Task Should_KeepCompletedTorrentPlaybackAvailableWhileWaitingForImport(DownloadSource source, IntegrationApplication sut)
    {
        // Arrange
        await using var application = sut;
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        deadline.CancelAfter(TimeSpan.FromMinutes(2));
        var cancellationToken = deadline.Token;
        await sut.StartAsync(source, cancellationToken, completed: true);
        await sut.DiscoverAsync(source.Discover, cancellationToken);
        var original = sut.Store.All().Single();
        var waitingStatus = "Waiting for import";
        var completedProgress = 1d;

        try
        {
            // Act
            await sut.DiscoverAsync(source.Discover, cancellationToken);
            await sut.DiscoverAsync(source.Discover, cancellationToken);
            await sut.RestartAsync();
            await sut.DiscoverAsync(source.Discover, cancellationToken);
            var entries = sut.Store.All();
            var waiting = entries.Single();
            var bytes = await sut.ReadAsync(waiting, 0, source.Media.Length, cancellationToken);

            // Assert
            entries.Should().ContainSingle();
            waiting.Status.Should().Be(waitingStatus);
            waiting.Progress.Should().Be(completedProgress);
            waiting.CompletedPath.Should().BeNull();
            waiting.Id.Should().Be(original.Id);
            waiting.StreamPath.Should().Be(original.StreamPath);
            string.Equals(waiting.Secret, original.Secret, StringComparison.Ordinal).Should().BeTrue("repeated polls and restart must not issue a new capability");
            File.Exists(sut.Swarm.LocalDownloadPath).Should().BeTrue();
            bytes.Should().Equal(source.Media);
        }
        catch
        {
            output.WriteLine(await sut.DiagnosticsAsync());
            throw;
        }
    }
}
