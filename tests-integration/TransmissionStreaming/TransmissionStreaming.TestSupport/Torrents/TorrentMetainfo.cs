namespace TransmissionStreaming.TestSupport.Torrents;

using System.Security.Cryptography;
using System.Text;

/// <summary>Builds only the private, single-file BitTorrent v1 shape used by the fixtures.</summary>
internal sealed record TorrentMetainfo(byte[] Bytes, string Hash)
{
    public static TorrentMetainfo Create(byte[] media, string fileName, int pieceSize)
    {
        using var hashes = new MemoryStream();
        for (var offset = 0; offset < media.Length; offset += pieceSize)
        {
            hashes.Write(SHA1.HashData(media.AsSpan(offset, Math.Min(pieceSize, media.Length - offset))));
        }

        using var info = new MemoryStream();
        info.Write("d6:length"u8);
        Integer(info, media.Length);
        info.Write("4:name"u8);
        WriteBytes(info, Encoding.UTF8.GetBytes(fileName));
        info.Write("12:piece length"u8);
        Integer(info, pieceSize);
        info.Write("6:pieces"u8);
        WriteBytes(info, hashes.ToArray());
        info.Write("7:privatei1ee"u8);
        var encodedInfo = info.ToArray();

        using var torrent = new MemoryStream();
        torrent.Write("d8:announce"u8);
        WriteBytes(torrent, Encoding.ASCII.GetBytes(new UriBuilder("http", SwarmSettings.SeedHost, SwarmSettings.HttpPort, "/announce").Uri.AbsoluteUri));
        torrent.Write("4:info"u8);
        torrent.Write(encodedInfo);
        torrent.WriteByte((byte)'e');
        return new TorrentMetainfo(torrent.ToArray(), Convert.ToHexStringLower(SHA1.HashData(encodedInfo)));
    }

    private static void WriteBytes(Stream stream, byte[] value)
    {
        stream.Write(Encoding.ASCII.GetBytes(value.Length + ":"));
        stream.Write(value);
    }

    private static void Integer(Stream stream, int value)
    {
        stream.Write(Encoding.ASCII.GetBytes("i" + value + "e"));
    }
}
