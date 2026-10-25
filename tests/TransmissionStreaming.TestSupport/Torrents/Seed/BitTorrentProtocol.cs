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
        await stream.ReadExactlyAsync
        (
            buffer: incoming,
            cancellationToken: cancellationToken
        );
        var accepted = Matches
        (
            bytes: incoming,
            hash: hash
        );
        if (!accepted)
        {
            return false;
        }

        var response = CreateHandshake(hash);
        await stream.WriteAsync
        (
            buffer: response,
            cancellationToken: cancellationToken
        );
        return true;
    }

    public static async Task SendAsync(NetworkStream stream, byte identifier, byte[] body, CancellationToken cancellationToken)
    {
        var frame = new byte[5 + body.Length];
        BinaryPrimitives.WriteInt32BigEndian
        (
            destination: frame,
            value: body.Length + 1
        );
        frame[4] = identifier;
        body.CopyTo
        (
            array: frame,
            index: 5
        );
        await stream.WriteAsync
        (
            buffer: frame,
            cancellationToken: cancellationToken
        );
    }

    private static byte[] CreateHandshake(string hash)
    {
        var bytes = new byte[HandshakeLength];
        bytes[0] = 19;
        "BitTorrent protocol"u8.CopyTo(bytes.AsSpan(1));
        var infoHash = Convert.FromHexString(hash);
        infoHash.CopyTo
        (
            array: bytes,
            index: InfoHashOffset
        );
        "-TS0001-012345678901"u8.CopyTo(bytes.AsSpan(PeerIdOffset));
        return bytes;
    }

    private static bool Matches(byte[] bytes, string hash)
    {
        if (bytes[0] != 19)
        {
            return false;
        }

        var protocol = bytes.AsSpan
        (
            start: 1,
            length: 19
        );
        if (!protocol.SequenceEqual("BitTorrent protocol"u8))
        {
            return false;
        }

        var receivedHash = bytes.AsSpan
        (
            start: InfoHashOffset,
            length: 20
        );
        var expectedHash = Convert.FromHexString(hash);
        return receivedHash.SequenceEqual(expectedHash);
    }
}
