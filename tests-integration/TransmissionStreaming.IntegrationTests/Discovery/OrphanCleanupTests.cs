namespace TransmissionStreaming.IntegrationTests.Discovery;

using Fixtures;
using FluentAssertions;
using Xunit;

public sealed class OrphanCleanupTests(ITestOutputHelper output)
{
    [Theory, AutoMoqData]
    internal async Task Should_RemoveACancelledDownloadWithoutRecreatingItsProjectionAfterRestart(DownloadSource source, IntegrationApplication sut)
    {
        // Arrange
        await using var application = sut;
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        deadline.CancelAfter(TimeSpan.FromMinutes(2));
        var cancellationToken = deadline.Token;
        await sut.StartAsync(source, cancellationToken);
        await sut.Mediator.Send(source.Discover, cancellationToken);
        var original = sut.Store.All().Single();
        var sidecar = Path.ChangeExtension(original.StreamPath, ".nfo");

        try
        {
            // Act
            await sut.Arr.SetRemovedAsync(true, cancellationToken);
            await sut.Swarm.RemoveAsync(cancellationToken);
            File.Delete(sut.Swarm.LocalPartialPath);
            await sut.Mediator.Send(source.Discover, cancellationToken);
            await sut.RestartAsync();
            await sut.Mediator.Send(source.Discover, cancellationToken);
            var remaining = sut.Store.All();

            // Assert
            remaining.Should().BeEmpty();
            sut.Store.Get(original.Id).Should().BeNull();
            File.Exists(original.StreamPath).Should().BeFalse();
            File.Exists(sidecar).Should().BeFalse();
        }
        catch
        {
            output.WriteLine(await sut.DiagnosticsAsync());
            throw;
        }
    }

    [Theory, AutoMoqData]
    internal async Task Should_KeepStateDuringAnArrOutageAndCleanUpWhenItRecovers(DownloadSource source, IntegrationApplication sut)
    {
        // Arrange
        await using var application = sut;
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        deadline.CancelAfter(TimeSpan.FromMinutes(2));
        var cancellationToken = deadline.Token;
        await sut.StartAsync(source, cancellationToken);
        await sut.Mediator.Send(source.Discover, cancellationToken);
        var original = sut.Store.All().Single();

        try
        {
            // Act
            await sut.Swarm.RemoveAsync(cancellationToken);
            await sut.Arr.SetRemovedAsync(true, cancellationToken);
            await sut.Arr.SetFailureAsync(true, cancellationToken);
            var failure = await Record.ExceptionAsync(async () => await sut.Mediator.Send(source.Discover, cancellationToken));
            var retained = sut.Store.Get(original.Id);
            var retainedFile = File.Exists(original.StreamPath);
            await sut.Arr.SetFailureAsync(false, cancellationToken);
            await sut.Mediator.Send(source.Discover, cancellationToken);

            // Assert
            failure.Should().BeOfType<HttpRequestException>();
            retained.Should().NotBeNull();
            retainedFile.Should().BeTrue();
            sut.Store.All().Should().BeEmpty();
            File.Exists(original.StreamPath).Should().BeFalse();
        }
        catch
        {
            output.WriteLine(await sut.DiagnosticsAsync());
            throw;
        }
    }
}
