namespace TransmissionStreaming.TestSupport.Torrents.Seed;

using System.Buffers.Binary;
using System.Net.Sockets;

internal static class BitTorrentProtocol
{
    public static async Task<bool> HandshakeAsync(NetworkStream stream, string hash, CancellationToken cancellationToken)
    {
        var handshake = new byte[68];
        await stream.ReadExactlyAsync(handshake, cancellationToken);
        var expectedHash = Convert.FromHexString(hash);
        if (handshake[0] != 19 || !handshake.AsSpan(1, 19).SequenceEqual("BitTorrent protocol"u8) ||
            !handshake.AsSpan(28, 20).SequenceEqual(expectedHash))
        {
            return false;
        }
        var reply = new byte[68];
        reply[0] = 19;
        "BitTorrent protocol"u8.CopyTo(reply.AsSpan(1));
        expectedHash.CopyTo(reply, 28);
        "-TS0001-012345678901"u8.CopyTo(reply.AsSpan(48));
        await stream.WriteAsync(reply, cancellationToken);
        return true;
    }

    public static async Task SendAsync(NetworkStream stream, byte identifier, byte[] body, CancellationToken cancellationToken)
    {
        var frame = new byte[5 + body.Length];
        BinaryPrimitives.WriteInt32BigEndian(frame, body.Length + 1);
        frame[4] = identifier;
        body.CopyTo(frame, 5);
        await stream.WriteAsync(frame, cancellationToken);
    }
}
