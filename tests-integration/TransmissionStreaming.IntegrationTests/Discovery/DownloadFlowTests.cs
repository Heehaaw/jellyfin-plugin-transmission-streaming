namespace TransmissionStreaming.IntegrationTests.Discovery;

using AutoFixture;
using AutoFixture.AutoMoq;
using Core.Arr;
using Fixtures;
using FluentAssertions;
using global::TransmissionStreaming.TestSupport.Testing;
using Xunit;

public sealed class DownloadFlowTests(ITestOutputHelper output)
{
    [Theory, AutoMoqData]
    internal async Task Should_DiscoverAndReadVerifiedPieces(DownloadSource source, IntegrationApplication sut)
    {
        // Arrange
        await using var application = sut;
        try
        {
            using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(2));
            var cancellationToken = deadline.Token;
            await sut.StartAsync(source, cancellationToken);
            await sut.Swarm.AllowFirstPieceAsync(cancellationToken);

            // Act
            await sut.Mediator.Send(source.Discover, cancellationToken);
            var projection = sut.Store.All().Single();
            var received = await sut.ReadAsync(projection, 0, sut.Swarm.PieceSize, cancellationToken);
            var download = await sut.Swarm.ProgressAsync(cancellationToken);

            // Assert
            projection.TorrentHash.Should().Be(sut.Swarm.Hash);
            projection.SourceName.Should().Be(source.FileName);
            projection.Length.Should().Be(source.Media.Length);
            projection.Identity.Title.Should().Be(sut.Swarm.Title);
            File.Exists(projection.StreamPath).Should().BeTrue();
            projection.CompletedPath.Should().BeNull();
            download.Complete.Should().BeFalse();
            received.Should().Equal(source.Media.Take(sut.Swarm.PieceSize));
        }
        catch
        {
            output.WriteLine(await sut.DiagnosticsAsync());
            throw;
        }
    }

    [Theory, AutoMoqData]
    internal async Task Should_PreserveIdentityAndPlaybackAfterImportAndRestart(DownloadSource source, IntegrationApplication sut)
    {
        // Arrange
        await using var application = sut;
        try
        {
            using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(2));
            var cancellationToken = deadline.Token;
            await sut.StartAsync(source, cancellationToken);
            sut.UseIndependentArrMounts();
            await sut.Mediator.Send(source.Discover, cancellationToken);
            var original = sut.Store.All().Single();
            var expectedStatus = "Waiting for Jellyfin handoff";
            await sut.Swarm.AllowAllPiecesAsync(cancellationToken);

            // Act
            var importedPath = await sut.ImportAsync(cancellationToken);
            await sut.Mediator.Send(source.Discover, cancellationToken);
            await sut.Swarm.RemoveAsync(cancellationToken);
            await sut.RestartAsync();
            await sut.Mediator.Send(source.Discover, cancellationToken);
            var restored = sut.Store.All().Single();
            var received = await sut.ReadAsync(restored, 0, source.Media.Length, cancellationToken);

            // Assert
            restored.Id.Should().Be(original.Id);
            string.Equals(restored.Secret, original.Secret, StringComparison.Ordinal).Should().BeTrue("the existing capability must survive restart");
            restored.StreamPath.Should().Be(original.StreamPath);
            restored.CompletedPath.Should().Be(importedPath);
            restored.Status.Should().Be(expectedStatus);
            received.Should().Equal(source.Media);
            File.Exists(importedPath).Should().BeTrue();
            File.Exists(sut.Swarm.LocalDownloadPath).Should().BeFalse();
        }
        catch
        {
            output.WriteLine(await sut.DiagnosticsAsync());
            throw;
        }
    }

    [Theory, AutoMoqData]
    internal async Task Should_RejectImportFromAnotherTorrent(DownloadSource source, IntegrationApplication sut, Guid unrelatedTorrent)
    {
        // Arrange
        await using var application = sut;
        try
        {
            using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(2));
            var cancellationToken = deadline.Token;
            await sut.StartAsync(source, cancellationToken);
            await sut.Mediator.Send(source.Discover, cancellationToken);
            var original = sut.Store.All().Single();
            await sut.Swarm.AllowAllPiecesAsync(cancellationToken);
            var importedPath = await sut.ImportAsync(cancellationToken);
            await sut.Arr.PublishUnrelatedImportAsync(sut.Swarm.RemotePath(importedPath), unrelatedTorrent.ToString("N"), cancellationToken);

            // Act
            await sut.Mediator.Send(source.Discover, cancellationToken);
            var afterUnrelatedImport = sut.Store.All().Single();
            await sut.Arr.PublishImportAsync(sut.Swarm.RemotePath(importedPath), cancellationToken);
            await sut.Mediator.Send(source.Discover, cancellationToken);
            var afterExactImport = sut.Store.All().Single();

            // Assert
            afterUnrelatedImport.CompletedPath.Should().BeNull();
            afterUnrelatedImport.Id.Should().Be(original.Id);
            afterExactImport.CompletedPath.Should().Be(importedPath);
            afterExactImport.Id.Should().Be(original.Id);
        }
        catch
        {
            output.WriteLine(await sut.DiagnosticsAsync());
            throw;
        }
    }

    [Theory, AutoMoqData]
    internal async Task Should_PreserveSavedProjectionsWhenArrFails(DownloadSource source, IntegrationApplication sut)
    {
        // Arrange
        await using var application = sut;
        try
        {
            using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(2));
            var cancellationToken = deadline.Token;
            await sut.StartAsync(source, cancellationToken);
            await sut.Mediator.Send(source.Discover, cancellationToken);
            var original = sut.Store.All().Single();
            await sut.Arr.SetFailureAsync(true, cancellationToken);

            // Act
            var failedDiscovery = async () => await sut.Mediator.Send(source.Discover, cancellationToken);
            var failure = await Record.ExceptionAsync(failedDiscovery);
            await sut.RestartAsync();
            var restored = sut.Store.All().Single();
            await sut.Arr.SetFailureAsync(false, cancellationToken);
            await sut.Mediator.Send(source.Discover, cancellationToken);
            var recovered = sut.Store.All().Single();

            // Assert
            failure.Should().BeOfType<HttpRequestException>();
            restored.Should().BeEquivalentTo(original, options => options.Excluding(projection => projection.Secret));
            string.Equals(restored.Secret, original.Secret, StringComparison.Ordinal).Should().BeTrue("failed discovery must not rotate the capability");
            File.Exists(restored.StreamPath).Should().BeTrue();
            recovered.Should().BeEquivalentTo(original, options => options.Excluding(projection => projection.Secret));
            string.Equals(recovered.Secret, original.Secret, StringComparison.Ordinal).Should().BeTrue("recovery must retain the original capability");
        }
        catch
        {
            output.WriteLine(await sut.DiagnosticsAsync());
            throw;
        }
    }

    [Theory, AutoMoqData]
    internal async Task Should_NotDiscoverWhenDisabled(DownloadSource source, IntegrationApplication sut)
    {
        // Arrange
        await using var application = sut;
        try
        {
            using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(2));
            var cancellationToken = deadline.Token;
            await sut.StartAsync(source, cancellationToken);
            sut.SetEnabled(false);

            // Act
            await sut.Mediator.Send(source.Discover, cancellationToken);
            var projections = sut.Store.All();

            // Assert
            projections.Should().BeEmpty();
            Directory.Exists(Path.Combine(sut.Workspace, "projections")).Should().BeFalse();
        }
        catch
        {
            output.WriteLine(await sut.DiagnosticsAsync());
            throw;
        }
    }

    [Theory, AutoMoqData]
    internal async Task Should_ResolveSonarrEpisodeThroughImport(DownloadSource source, IntegrationApplication sut)
    {
        // Arrange
        await using var application = sut;
        try
        {
            using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(2));
            var cancellationToken = deadline.Token;
            var episodeSource = source with { FileName = Path.GetFileNameWithoutExtension(source.FileName) + ".S01E02.mkv" };
            var expectedSeason = 1;
            var expectedEpisode = 2;
            await sut.StartAsync(episodeSource, cancellationToken);
            sut.UseIndependentArrMounts();
            await sut.Arr.SetEpisodeAsync(true, cancellationToken);

            // Act
            await sut.Mediator.Send(source.Discover, cancellationToken);
            var original = sut.Store.All().Single();
            await sut.Swarm.AllowAllPiecesAsync(cancellationToken);
            var importedPath = await sut.ImportAsync(cancellationToken);
            await sut.Mediator.Send(source.Discover, cancellationToken);
            await sut.Swarm.RemoveAsync(cancellationToken);
            var imported = sut.Store.All().Single();
            var received = await sut.ReadAsync(imported, 0, episodeSource.Media.Length, cancellationToken);

            // Assert
            imported.Identity.Kind.Should().Be(MediaKind.Episode);
            imported.Identity.SeasonNumber.Should().Be(expectedSeason);
            imported.Identity.EpisodeNumber.Should().Be(expectedEpisode);
            imported.Id.Should().Be(original.Id);
            imported.CompletedPath.Should().Be(importedPath);
            received.Should().Equal(episodeSource.Media);
        }
        catch
        {
            output.WriteLine(await sut.DiagnosticsAsync());
            throw;
        }
    }

    private sealed class AutoMoqDataAttribute() : AutoDataAttribute(() =>
    {
        var fixture = new Fixture { RepeatCount = 1 };
        fixture.Customize(new AutoMoqCustomization { ConfigureMembers = true, GenerateDelegates = true });
        // Every torrent source must span several complete pieces and use a legal video filename.
        fixture.Customize<DownloadSource>(composer => composer
            .With(source => source.Media, fixture.CreateMany<byte>(128 * 1024).ToArray())
            .With(source => source.FileName, fixture.Create<string>() + ".mkv"));
        return fixture;
    });
}
