namespace TransmissionStreaming.Infrastructure.Tests.Discovery;

using FluentAssertions;
using global::TransmissionStreaming.Core.Discovery;
using global::TransmissionStreaming.Infrastructure.Discovery;
using Xunit;

public sealed class DiscoveryStatusStoreTests
{
    [Theory, AutoMoqData]
    internal async Task Should_PublishWholeImmutableStatusSnapshots(DiscoveryStatus source, DiscoveryStatusStore sut)
    {
        // Arrange
        var original = sut.Get();

        // Act
        await Task.Run(() => sut.Set(source));
        var published = sut.Get();

        // Assert
        published.Should().BeSameAs(source);
        original.Should().NotBeSameAs(published);
        original.LastFailure.Should().BeNull();
    }
}
