namespace TransmissionStreaming.Core.Tests.Streaming;

using FluentAssertions;
using global::TransmissionStreaming.Core.Streaming;
using Xunit;

public sealed class PieceMapTests
{
    [Theory, AutoMoqData]
    public void Should_StopAtTheFirstMissingPiece(byte seed)
    {
        // Arrange
        var pieceSize = (long)seed + 8;
        var offset = pieceSize / 2;
        var expected = (int)(pieceSize - offset);
        byte[] pieces = [0x80];

        // Act
        var available = PieceMap.AvailableBytes(pieces, pieceSize, offset, (int)pieceSize);

        // Assert
        available.Should().Be(expected);
    }

    [Theory, AutoMoqData]
    public void Should_CrossBitfieldByteBoundaries(byte seed)
    {
        // Arrange
        var pieceSize = (long)seed + 8;
        var offset = pieceSize * 7;
        var requested = (int)pieceSize * 2;
        byte[] pieces = [0x01, 0x80];

        // Act
        var available = PieceMap.AvailableBytes(pieces, pieceSize, offset, requested);

        // Assert
        available.Should().Be(requested);
    }

    [Theory, AutoMoqData]
    public void Should_CapAvailabilityAtTheRequestedLength(byte seed)
    {
        // Arrange
        var pieceSize = (long)seed + 8;
        var requested = (int)pieceSize / 2;
        byte[] pieces = [0xff];

        // Act
        var available = PieceMap.AvailableBytes(pieces, pieceSize, 0, requested);

        // Assert
        available.Should().Be(requested);
    }

    [Theory, AutoMoqData]
    public void Should_RefuseMissingBitfieldBytes(byte seed)
    {
        // Arrange
        var pieceSize = (long)seed + 8;
        var offset = pieceSize * 8;
        byte[] pieces = [0xff];

        // Act
        var available = PieceMap.AvailableBytes(pieces, pieceSize, offset, (int)pieceSize);

        // Assert
        available.Should().Be(0);
    }

    [Theory, AutoMoqData]
    public void Should_RejectInvalidReadGeometry(byte seed)
    {
        // Arrange
        var pieceSize = (long)seed + 8;
        byte[] pieces = [0xff];

        // Act
        var noPieceSize = PieceMap.AvailableBytes(pieces, 0, 0, 1);
        var negativeOffset = PieceMap.AvailableBytes(pieces, pieceSize, -1, 1);
        var noRequestedBytes = PieceMap.AvailableBytes(pieces, pieceSize, 0, 0);

        // Assert
        noPieceSize.Should().Be(0);
        negativeOffset.Should().Be(0);
        noRequestedBytes.Should().Be(0);
    }
}
