namespace TransmissionStreaming.Core.Tests.Transmission;

using FluentAssertions;
using global::TransmissionStreaming.Core.Transmission;
using Xunit;

public sealed class TorrentSnapshotTests
{
    [Theory, AutoMoqData]
    public void Should_SumOnlyFilesBeforeTheSelectedFile(TorrentSnapshot source)
    {
        // Arrange
        var selectedIndex = source.Files.Count - 1;
        var expectedOffset = source.Files.Take(selectedIndex).Sum(file => file.Length);

        // Act
        var offset = source.GetFileOffset(selectedIndex);

        // Assert
        offset.Should().Be(expectedOffset);
        source.GetFileOffset(0).Should().Be(0);
    }

    [Theory, AutoMoqData]
    public void Should_RejectAnIndexOutsideTheTorrent(TorrentSnapshot source)
    {
        // Arrange
        var outside = source.Files.Count;

        // Act
        var pastEnd = () => source.GetFileOffset(outside);
        var beforeStart = () => source.GetFileOffset(-1);

        // Assert
        pastEnd.Should().Throw<ArgumentOutOfRangeException>();
        beforeStart.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory, AutoMoqData]
    public void Should_RejectOffsetOverflow(TorrentSnapshot source)
    {
        // Arrange
        source.Files[0].Length = long.MaxValue;
        source.Files[1].Length = 1;

        // Act
        var action = () => source.GetFileOffset(2);

        // Assert
        action.Should().Throw<OverflowException>();
    }
}
