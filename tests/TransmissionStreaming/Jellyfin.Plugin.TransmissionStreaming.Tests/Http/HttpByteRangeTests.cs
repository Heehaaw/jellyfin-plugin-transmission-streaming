namespace Jellyfin.Plugin.TransmissionStreaming.Tests.Http;

using AutoFixture;
using AutoFixture.AutoMoq;
using AutoFixture.Xunit2;
using FluentAssertions;
using global::TransmissionStreaming.Core.Streaming;
using Xunit;
using Jellyfin.Plugin.TransmissionStreaming.Http;

public sealed class HttpByteRangeTests
{
    private sealed class AutoMoqDataAttribute() : AutoDataAttribute(() =>
        {
            var fixture = new Fixture
            {
                RepeatCount = 1
            };
            fixture.Customize(
                new AutoMoqCustomization
                {
                    ConfigureMembers = true,
                    GenerateDelegates = true
                }
            );
            // Every range test needs a nonempty file with room for distinct start/end offsets.
            fixture.Register(() => (long)fixture.Create<int>() + 8);
            return fixture;
        }
    );

    [Theory, AutoMoqData]
    public void Should_ReturnWholeFileWithoutRange(long length)
    {
        // Arrange
        var expectedStart = 0L;

        // Act
        var accepted = HttpByteRange.TryParse(null, length, out var range);

        // Assert
        accepted.Should().BeTrue();
        range.Start.Should().Be(expectedStart);
        range.Count.Should().Be(length);
        range.Partial.Should().BeFalse();
    }

    [Theory, AutoMoqData]
    public void Should_InterpretInclusiveBounds(long length)
    {
        // Arrange
        var start = length / 3;
        var end = length * 2 / 3;
        var expectedCount = end - start + 1;

        // Act
        var accepted = HttpByteRange.TryParse($"bytes={start}-{end}", length, out var range);

        // Assert
        accepted.Should().BeTrue();
        range.Start.Should().Be(start);
        range.Count.Should().Be(expectedCount);
        range.Partial.Should().BeTrue();
    }

    [Theory, AutoMoqData]
    public void Should_ReadSuffixFromTheEnd(long length)
    {
        // Arrange
        var count = length / 2;
        var expectedStart = length - count;

        // Act
        var accepted = HttpByteRange.TryParse($"bytes=-{count}", length, out var range);

        // Assert
        accepted.Should().BeTrue();
        range.Start.Should().Be(expectedStart);
        range.Count.Should().Be(count);
    }

    [Theory, AutoMoqData]
    public void Should_ClampAnOversizedSuffix(long length)
    {
        // Arrange
        var count = length + 1;
        var expectedStart = 0L;

        // Act
        var accepted = HttpByteRange.TryParse($"bytes=-{count}", length, out var range);

        // Assert
        accepted.Should().BeTrue();
        range.Start.Should().Be(expectedStart);
        range.Count.Should().Be(length);
    }

    [Theory, AutoMoqData]
    public void Should_ReadOpenEndedRange(long length)
    {
        // Arrange
        var start = length / 2;
        var expectedCount = length - start;

        // Act
        var accepted = HttpByteRange.TryParse($"bytes={start}-", length, out var range);

        // Assert
        accepted.Should().BeTrue();
        range.Start.Should().Be(start);
        range.Count.Should().Be(expectedCount);
    }

    [Theory, AutoMoqData]
    public void Should_ClampAnEndPastTheFile(long length)
    {
        // Arrange
        var start = length / 2;
        var expectedCount = length - start;

        // Act
        var accepted = HttpByteRange.TryParse($"bytes={start}-{length + 20}", length, out var range);

        // Assert
        accepted.Should().BeTrue();
        range.Start.Should().Be(start);
        range.Count.Should().Be(expectedCount);
    }

    [Theory, AutoMoqData]
    public void Should_RejectUnsatisfiableRange(long length)
    {
        // Arrange
        var start = length;

        // Act
        var accepted = HttpByteRange.TryParse($"bytes={start}-", length, out _);

        // Assert
        accepted.Should().BeFalse();
    }

    [Theory, AutoMoqData]
    public void Should_RejectReversedBounds(long length)
    {
        // Arrange
        var start = length - 1;
        var end = start - 1;

        // Act
        var accepted = HttpByteRange.TryParse($"bytes={start}-{end}", length, out _);

        // Assert
        accepted.Should().BeFalse();
    }

    [Theory, AutoMoqData]
    public void Should_RejectMultipartRanges(long length)
    {
        // Arrange
        var end = length - 1;

        // Act
        var accepted = HttpByteRange.TryParse($"bytes=0-1,2-{end}", length, out _);

        // Assert
        accepted.Should().BeFalse();
    }

    [Theory, AutoMoqData]
    public void Should_RejectMalformedNumbers(string invalid, long length)
    {
        // Arrange
        var header = $"bytes={invalid}-";

        // Act
        var accepted = HttpByteRange.TryParse(header, length, out _);

        // Assert
        accepted.Should().BeFalse();
    }
}
