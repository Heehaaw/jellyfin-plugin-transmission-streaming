namespace TransmissionStreaming.TestSupport.Torrents.Seed;

using System.Buffers.Binary;
using System.Net.Sockets;

internal static class BitTorrentTransfer
{
    public static async Task CopyAsync(NetworkStream stream, SeedScenario control, CancellationToken cancellationToken)
    {
        int announced;
        lock (control.Gate)
        {
            announced = control.State.Allowed;
        }

        // BitTorrent numbers pieces from the most significant bit of each byte.
        var bitfield = new byte[(control.PieceCount + 7) / 8];
        for (var piece = 0; piece < announced; piece++)
        {
            bitfield[piece / 8] |= (byte)(1 << (7 - piece % 8));
        }

        await BitTorrentProtocol.SendAsync(stream, 5, bitfield, cancellationToken);
        await BitTorrentProtocol.SendAsync(stream, 1, [], cancellationToken);
        var pending = new List<BlockRequest>();
        while (!cancellationToken.IsCancellationRequested)
        {
            int allowed;
            lock (control.Gate)
            {
                allowed = control.State.Allowed;
            }

            // Release new pieces while the downloader is waiting on earlier requests.
            for (var piece = announced; piece < allowed; piece++)
            {
                var have = new byte[4];
                BinaryPrimitives.WriteInt32BigEndian(have, piece);
                await BitTorrentProtocol.SendAsync(stream, 4, have, cancellationToken);
            }

            announced = allowed;
            foreach (var request in pending.ToArray())
            {
                if (request.Piece < allowed)
                {
                    var block = new byte[8 + request.Length];
                    BinaryPrimitives.WriteInt32BigEndian(block, request.Piece);
                    BinaryPrimitives.WriteInt32BigEndian(block.AsSpan(4), request.Begin);
                    var sourceOffset = request.Piece * control.Configuration.PieceSize + request.Begin;
                    control.Media.AsSpan(sourceOffset, request.Length).CopyTo(block.AsSpan(8));
                    await BitTorrentProtocol.SendAsync(stream, 7, block, cancellationToken);
                    pending.Remove(request);
                }
            }

            if (!stream.DataAvailable)
            {
                await Task.Delay(50, cancellationToken);
                continue;
            }

            var header = new byte[4];
            await stream.ReadExactlyAsync(header, cancellationToken);
            var length = BinaryPrimitives.ReadInt32BigEndian(header);
            if (length < 0 || length > 65536)
            {
                return;
            }

            if (length == 0)
            {
                continue;
            }

            var frame = new byte[length];
            await stream.ReadExactlyAsync(frame, cancellationToken);
            if (length == 13 && frame[0] == 6)
            {
                var requestedPiece = BinaryPrimitives.ReadInt32BigEndian(frame.AsSpan(1));
                var requestedBegin = BinaryPrimitives.ReadInt32BigEndian(frame.AsSpan(5));
                var requestedLength = BinaryPrimitives.ReadInt32BigEndian(frame.AsSpan(9));
                var request = new BlockRequest(requestedPiece, requestedBegin, requestedLength);
                if (request.Piece >= 0 && request.Piece < control.PieceCount && request.Begin >= 0 && request.Length > 0 && request.Length <= 16384 &&
                    request.Begin + request.Length <= Math.Min(
                        control.Configuration.PieceSize,
                        control.Media.Length - request.Piece * control.Configuration.PieceSize
                    ))
                {
                    pending.Add(request);
                }
            }
        }
    }

    private sealed record BlockRequest(int Piece, int Begin, int Length);
}
