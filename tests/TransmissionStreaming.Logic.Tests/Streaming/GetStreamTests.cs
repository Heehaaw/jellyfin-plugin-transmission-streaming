namespace TransmissionStreaming.Logic.Tests.Streaming;

using AutoFixture;
using AutoFixture.AutoMoq;
using AutoFixture.Xunit2;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;
using TransmissionStreaming.Core.Configuration;
using TransmissionStreaming.Core.Projection;
using TransmissionStreaming.Logic.Streaming;
using Xunit;

public sealed class GetStreamTests
{
    [Theory, AutoMoqData]
    internal async Task Should_ReturnAuthorizedProjection(AccessScenario source,
        [Frozen] Mock<IOptionsMonitor<StreamingOptions>> options, [Frozen] Mock<IProjectionStore> store, GetStream.Handler sut)
    {
        // Arrange
        var query = source.Query with { Id = source.Entry.Id, Key = source.Entry.Secret };
        options.SetupGet(value => value.CurrentValue).Returns(source.Options with { Enabled = true });
        store.Setup(value => value.Get(query.Id)).Returns(source.Entry);

        // Act
        var result = await sut.Handle(query, CancellationToken.None);

        // Assert
        result.Should().BeSameAs(source.Entry);
        query.ToString().Should().NotContain(source.Entry.Secret);
    }

    [Theory, AutoMoqData]
    internal async Task Should_RejectDisabledStreaming(AccessScenario source,
        [Frozen] Mock<IOptionsMonitor<StreamingOptions>> options, [Frozen] Mock<IProjectionStore> store, GetStream.Handler sut)
    {
        // Arrange
        var query = source.Query with { Id = source.Entry.Id, Key = source.Entry.Secret };
        options.SetupGet(value => value.CurrentValue).Returns(source.Options with { Enabled = false });

        // Act
        var result = await sut.Handle(query, CancellationToken.None);

        // Assert
        result.Should().BeNull();
        store.Verify(value => value.Get(It.IsAny<string>()), Times.Never);
    }

    [Theory, AutoMoqData]
    internal async Task Should_RejectIncorrectCapability(AccessScenario source,
        [Frozen] Mock<IOptionsMonitor<StreamingOptions>> options, [Frozen] Mock<IProjectionStore> store, GetStream.Handler sut)
    {
        // Arrange
        var first = source.Entry.Secret[0] == 'A' ? 'B' : 'A';
        var query = source.Query with { Id = source.Entry.Id, Key = first + source.Entry.Secret[1..] };
        options.SetupGet(value => value.CurrentValue).Returns(source.Options with { Enabled = true });
        store.Setup(value => value.Get(query.Id)).Returns(source.Entry);

        // Act
        var result = await sut.Handle(query, CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    [Theory, AutoMoqData]
    internal async Task Should_RejectMissingCapability(AccessScenario source,
        [Frozen] Mock<IOptionsMonitor<StreamingOptions>> options, [Frozen] Mock<IProjectionStore> store, GetStream.Handler sut)
    {
        // Arrange
        var query = source.Query with { Key = null };
        options.SetupGet(value => value.CurrentValue).Returns(source.Options with { Enabled = true });

        // Act
        var result = await sut.Handle(query, CancellationToken.None);

        // Assert
        result.Should().BeNull();
        store.Verify(value => value.Get(It.IsAny<string>()), Times.Never);
    }

    private sealed class AutoMoqDataAttribute() : AutoDataAttribute(() =>
    {
        var fixture = new Fixture { RepeatCount = 1 };
        fixture.Customize(new AutoMoqCustomization { ConfigureMembers = true, GenerateDelegates = true });
        return fixture;
    });
}

public sealed record AccessScenario(GetStream.Query Query, Projection Entry, StreamingOptions Options);
