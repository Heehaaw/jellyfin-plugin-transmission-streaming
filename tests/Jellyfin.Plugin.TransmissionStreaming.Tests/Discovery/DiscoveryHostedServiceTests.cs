namespace Jellyfin.Plugin.TransmissionStreaming.Tests.Discovery;

using AutoFixture;
using AutoFixture.AutoMoq;
using AutoFixture.Xunit2;
using FluentAssertions;
using global::TransmissionStreaming.Core.Configuration;
using global::TransmissionStreaming.Core.Discovery;
using Jellyfin.Plugin.TransmissionStreaming.Discovery;
using Mediator;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

public sealed class DiscoveryHostedServiceTests
{
    [Theory, AutoMoqData]
    internal async Task Should_PollOnlyAtTheConfiguredInterval(StreamingOptions source,
        [Frozen] ObservableFakeTimeProvider clock, [Frozen] Mock<IOptionsMonitor<StreamingOptions>> configuration,
        [Frozen] Mock<IMediator> mediator, [Frozen] Mock<IDiscoveryStatus> status, DiscoveryHostedService sut)
    {
        // Arrange
        using var worker = sut;
        configuration.SetupGet(value => value.CurrentValue).Returns(source with { Enabled = true });
        mediator.Setup(value => value.Send(It.IsAny<ICommand>(), It.IsAny<CancellationToken>())).Returns(ValueTask.FromResult(Unit.Value));
        var updates = new List<DiscoveryStatus>();
        status.Setup(value => value.Set(It.IsAny<DiscoveryStatus>())).Callback<DiscoveryStatus>(updates.Add);
        var expectedDelay = TimeSpan.FromSeconds(Math.Clamp(source.PollSeconds, 2, 300));
        var boundaryRemainder = TimeSpan.FromTicks(1);
        var startedAt = clock.GetUtcNow();
        var expectedPollCount = 2;

        // Act
        await worker.StartAsync(CancellationToken.None);
        var firstDelay = await clock.NextDelayAsync();
        clock.Advance(expectedDelay - boundaryRemainder);
        var beforeBoundary = updates.Count;
        clock.Advance(boundaryRemainder);
        var nextDelay = await clock.NextDelayAsync();
        await worker.StopAsync(CancellationToken.None);

        // Assert
        firstDelay.Should().Be(expectedDelay);
        nextDelay.Should().Be(expectedDelay);
        beforeBoundary.Should().Be(expectedPollCount - 1);
        updates.Should().HaveCount(expectedPollCount);
        updates[0].LastSuccessfulPoll.Should().Be(startedAt);
        updates[1].LastSuccessfulPoll.Should().Be(startedAt + expectedDelay);
    }

    [Theory, AutoMoqData]
    internal async Task Should_ResumeDiscoveryAfterItIsEnabled(StreamingOptions source,
        [Frozen] ObservableFakeTimeProvider clock, [Frozen] Mock<IOptionsMonitor<StreamingOptions>> configuration,
        [Frozen] Mock<IMediator> mediator, DiscoveryHostedService sut)
    {
        // Arrange
        using var worker = sut;
        var current = source with { Enabled = false };
        configuration.SetupGet(value => value.CurrentValue).Returns(() => current);
        mediator.Setup(value => value.Send(It.IsAny<ICommand>(), It.IsAny<CancellationToken>())).Returns(ValueTask.FromResult(Unit.Value));
        var expectedPollCount = 1;

        // Act
        await worker.StartAsync(CancellationToken.None);
        var delay = await clock.NextDelayAsync();
        var whileDisabled = mediator.Invocations.Count;
        current = source with { Enabled = true };
        clock.Advance(delay);
        await clock.NextDelayAsync();
        await worker.StopAsync(CancellationToken.None);

        // Assert
        whileDisabled.Should().Be(0);
        mediator.Verify(value => value.Send(It.IsAny<ICommand>(), It.IsAny<CancellationToken>()), Times.Exactly(expectedPollCount));
    }

    [Theory, AutoMoqData]
    internal async Task Should_RetainSuccessTimeAcrossFailureAndRetry(StreamingOptions source, DiscoveryStatus initial, HttpRequestException failure,
        [Frozen] ObservableFakeTimeProvider clock, [Frozen] Mock<IOptionsMonitor<StreamingOptions>> configuration,
        [Frozen] Mock<IMediator> mediator, [Frozen] Mock<IDiscoveryStatus> status, DiscoveryHostedService sut)
    {
        // Arrange
        using var worker = sut;
        configuration.SetupGet(value => value.CurrentValue).Returns(source with { Enabled = true });
        mediator.SetupSequence(value => value.Send(It.IsAny<ICommand>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.FromResult(Unit.Value))
            .Returns(ValueTask.FromException<Unit>(failure))
            .Returns(ValueTask.FromResult(Unit.Value));
        var latest = initial;
        status.Setup(value => value.Get()).Returns(() => latest);
        status.Setup(value => value.Set(It.IsAny<DiscoveryStatus>())).Callback<DiscoveryStatus>(value => latest = value);
        var firstSuccessAt = clock.GetUtcNow();

        // Act
        await worker.StartAsync(CancellationToken.None);
        var firstDelay = await clock.NextDelayAsync();
        clock.Advance(firstDelay);
        var retryDelay = await clock.NextDelayAsync();
        var failed = latest;
        clock.Advance(retryDelay);
        await clock.NextDelayAsync();
        await worker.StopAsync(CancellationToken.None);

        // Assert
        failed.LastSuccessfulPoll.Should().Be(firstSuccessAt);
        failed.LastFailure.Should().Be(failure.GetType().Name);
        latest.LastSuccessfulPoll.Should().Be(firstSuccessAt + firstDelay + retryDelay);
        latest.LastFailure.Should().BeNull();
    }

    private sealed class AutoMoqDataAttribute() : AutoDataAttribute(() =>
    {
        var fixture = new Fixture { RepeatCount = 1 };
        fixture.Customize(new AutoMoqCustomization { ConfigureMembers = true, GenerateDelegates = true });
        fixture.Customize<ObservableFakeTimeProvider>(composer => composer.OmitAutoProperties());
        fixture.Register<TimeProvider>(() => fixture.Create<ObservableFakeTimeProvider>());
        return fixture;
    });
}
