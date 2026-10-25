namespace TransmissionStreaming.IntegrationTests.Discovery;

using Core.Projection;
using Fixtures;
using FluentAssertions;
using Xunit;

public sealed class NativeHandoffTests(ITestOutputHelper output)
{
    [Theory, AutoMoqData]
    internal async Task Should_RetireOnlyAfterTheNativeItemIsIndexedAndPersistTheHistory(DownloadSource source, LibraryItem native, IntegrationApplication sut)
    {
        // Arrange
        await using var application = sut;
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        deadline.CancelAfter(TimeSpan.FromMinutes(2));
        var cancellationToken = deadline.Token;
        await sut.StartAsync(source, cancellationToken, completed: true);
        await sut.DiscoverAsync(source.Discover, cancellationToken);
        var original = sut.Store.All().Single();
        var sidecar = Path.ChangeExtension(original.StreamPath, ".nfo");

        try
        {
            // Act
            var importedPath = await sut.ImportAsync(cancellationToken);
            await sut.DiscoverAsync(source.Discover, cancellationToken);
            var seeding = sut.Store.All().Single();
            await sut.Swarm.RemoveAsync(cancellationToken);
            await sut.DiscoverAsync(source.Discover, cancellationToken);
            var pending = sut.Store.All().Single();
            var pendingFilesExist = File.Exists(original.StreamPath) && File.Exists(sidecar);
            sut.SetNativeHandoffReady(
                path: importedPath,
                result: native with
                {
                    IsPlaying = false
                }
            );
            await sut.DiscoverAsync(source.Discover, cancellationToken);
            var completed = sut.Store.Completed().Single();
            await sut.RestartAsync();
            await sut.DiscoverAsync(source.Discover, cancellationToken);
            var received = await File.ReadAllBytesAsync(importedPath, cancellationToken);

            // Assert
            seeding.CompletedPath.Should().Be(importedPath);
            seeding.HandoffPending.Should().BeFalse();
            pending.HandoffPending.Should().BeTrue();
            pendingFilesExist.Should().BeTrue();
            completed.Id.Should().Be(original.Id);
            completed.NativeItemId.Should().Be(native.Id);
            completed.Title.Should().Be(original.Identity.Title);
            sut.Store.All().Should().BeEmpty();
            sut.Store.Get(original.Id).Should().BeNull();
            sut.Store.Completed().Should().ContainSingle().Which.Should().Be(completed);
            File.Exists(original.StreamPath).Should().BeFalse();
            File.Exists(sidecar).Should().BeFalse();
            received.Should().Equal(source.Media);
        }
        catch
        {
            output.WriteLine(await sut.DiagnosticsAsync());
            throw;
        }
    }
}
