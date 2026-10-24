namespace TransmissionStreaming.Core.Streaming;

/// <summary>Reads BitTorrent's most-significant-bit-first availability map.</summary>
public static class PieceMap
{
    /// <summary>Returns how many bytes from the requested offset are verified, capped at the requested count.</summary>
    public static int AvailableBytes(ReadOnlySpan<byte> pieces, long pieceSize, long globalOffset, int requested)
    {
        if (pieceSize <= 0 || globalOffset < 0 || requested <= 0)
        {
            return 0;
        }

        var available = 0;
        while (available < requested)
        {
            var position = checked(globalOffset + available);
            var piece = position / pieceSize;
            if (piece / 8 >= pieces.Length || (pieces[(int)(piece / 8)] & (0x80 >> (int)(piece % 8))) == 0)
            {
                break;
            }

            available += (int)Math.Min(requested - available, pieceSize - position % pieceSize);
        }
        return available;
    }
}
