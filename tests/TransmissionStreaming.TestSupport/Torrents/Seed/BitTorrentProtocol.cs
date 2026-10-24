namespace TransmissionStreaming.TestSupport.Torrents.Seed;

using System.Buffers.Binary;
using System.Net.Sockets;

internal static class BitTorrentProtocol
{
    private const int HandshakeLength = 68;
    private const int InfoHashOffset = 28;
    private const int PeerIdOffset = 48;

    public static async Task<bool> HandshakeAsync(NetworkStream stream, string hash, CancellationToken cancellationToken)
    {
        var incoming = new byte[HandshakeLength];
        await stream.ReadExactlyAsync(incoming, cancellationToken);
        if (!Matches(incoming, hash))
        {
            return false;
        }
        await stream.WriteAsync(CreateHandshake(hash), cancellationToken);
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

    private static byte[] CreateHandshake(string hash)
    {
        var bytes = new byte[HandshakeLength];
        bytes[0] = 19;
        "BitTorrent protocol"u8.CopyTo(bytes.AsSpan(1));
        Convert.FromHexString(hash).CopyTo(bytes, InfoHashOffset);
        "-TS0001-012345678901"u8.CopyTo(bytes.AsSpan(PeerIdOffset));
        return bytes;
    }

    private static bool Matches(byte[] bytes, string hash)
    {
        return bytes[0] == 19 && bytes.AsSpan(1, 19).SequenceEqual("BitTorrent protocol"u8)
            && bytes.AsSpan(InfoHashOffset, 20).SequenceEqual(Convert.FromHexString(hash));
    }
}
