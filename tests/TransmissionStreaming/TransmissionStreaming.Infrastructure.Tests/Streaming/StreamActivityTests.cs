namespace TransmissionStreaming.Infrastructure.Tests.Streaming;

using FluentAssertions;
using global::TransmissionStreaming.Infrastructure.Streaming;
using Xunit;

public sealed class StreamActivityTests
{
    [Theory, AutoMoqData]
    internal void Should_CountOverlappingReadersIndependently(string firstId, string secondId, StreamActivity sut)
    {
        // Arrange
        using var first = sut.Open(firstId);
        using var second = sut.Open(firstId);
        using var other = sut.Open(secondId);

        // Act
        first.Dispose();
        first.Dispose();
        var remaining = sut.Count(firstId);
        second.Dispose();

        // Assert
        remaining.Should().Be(1, "disposing the same lease twice must release only its own reader");
        sut.Count(firstId).Should().Be(0);
        sut.Count(secondId).Should().Be(1);
    }

    [Theory, AutoMoqData]
    internal async Task Should_ReleaseAReaderOnlyOnceAcrossConcurrentDisposals(string id, StreamActivity sut)
    {
        // Arrange
        using var lease = sut.Open(id);
        using var remaining = sut.Open(id);

        // Act
        await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Task.Run(lease.Dispose)));

        // Assert
        sut.Count(id).Should().Be(1);
    }
}
