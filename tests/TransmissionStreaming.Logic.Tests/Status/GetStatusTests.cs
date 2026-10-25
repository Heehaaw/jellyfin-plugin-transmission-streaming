namespace TransmissionStreaming.Logic.Tests.Status;

using AutoFixture;
using AutoFixture.AutoMoq;
using AutoFixture.Xunit2;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;
using TransmissionStreaming.Core.Configuration;
using TransmissionStreaming.Core.Discovery;
using TransmissionStreaming.Core.Projection;
using TransmissionStreaming.Core.Streaming;
using TransmissionStreaming.Logic.Status;
using Xunit;

public sealed class GetStatusTests
{
    [Theory, AutoMoqData]
    internal async Task Should_ReportDiscoveryAndActivePlayback(StatusScenario source, CompletedProjection completed,
        [Frozen] Mock<IOptionsMonitor<StreamingOptions>> options, [Frozen] Mock<IDiscoveryStatus> discovery,
        [Frozen] Mock<IProjectionStore> store, [Frozen] Mock<IStreamActivity> activity, GetStatus.Handler sut)
    {
        // Arrange
        options.SetupGet(value => value.CurrentValue).Returns(source.Options);
        discovery.Setup(value => value.Get()).Returns(source.Discovery);
        store.Setup(value => value.All()).Returns([source.Entry]);
        store.Setup(value => value.Completed()).Returns([completed]);
        activity.Setup(value => value.Count(source.Entry.Id)).Returns(source.Readers);

        // Act
        var result = await sut.Handle(source.Query, CancellationToken.None);

        // Assert
        result.Enabled.Should().Be(source.Options.Enabled);
        result.LastSuccessfulPoll.Should().Be(source.Discovery.LastSuccessfulPoll);
        result.LastFailure.Should().Be(source.Discovery.LastFailure);
        result.Completed.Should().ContainSingle().Which.Should().Be(completed);
        var entry = result.Entries.Should().ContainSingle().Subject;
        entry.Id.Should().Be(source.Entry.Id);
        entry.Title.Should().Be(source.Entry.Identity.Title);
        entry.Progress.Should().Be(source.Entry.Progress);
        entry.Status.Should().Be(source.Entry.Status);
        entry.Readers.Should().Be(source.Readers);
        result.ToString().Should().NotContain(source.Entry.Secret).And.NotContain(source.Options.TransmissionPassword);
    }

    [Theory, AutoMoqData]
    internal async Task Should_CancelBeforeReadingState(GetStatus.Query source,
        [Frozen] Mock<IProjectionStore> store, GetStatus.Handler sut)
    {
        // Arrange
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        // Act
        var action = async () => await sut.Handle(source, cancellation.Token);

        // Assert
        await action.Should().ThrowAsync<OperationCanceledException>();
        store.Verify(value => value.All(), Times.Never);
    }

    private sealed class AutoMoqDataAttribute() : AutoDataAttribute(() =>
    {
        var fixture = new Fixture { RepeatCount = 1 };
        fixture.Customize(new AutoMoqCustomization { ConfigureMembers = true, GenerateDelegates = true });
        return fixture;
    });
}

public sealed record StatusScenario(GetStatus.Query Query, StreamingOptions Options, DiscoveryStatus Discovery, Projection Entry, int Readers);
