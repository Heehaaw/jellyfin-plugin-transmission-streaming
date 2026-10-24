namespace TransmissionStreaming.TestSupport.Torrents.Seed;

using System.Text;

internal static class Bencode
{
    public static byte[] Encode(SortedDictionary<string, object> values)
    {
        using var result = new MemoryStream();
        result.WriteByte((byte)'d');
        foreach (var (key, value) in values)
        {
            WriteBytes(Encoding.ASCII.GetBytes(key));
            if (value is int number)
            {
                result.Write(Encoding.ASCII.GetBytes("i" + number + "e"));
            }
            else
            {
                WriteBytes((byte[])value);
            }
        }
        result.WriteByte((byte)'e');
        return result.ToArray();

        void WriteBytes(byte[] bytes)
        {
            result.Write(Encoding.ASCII.GetBytes(bytes.Length + ":"));
            result.Write(bytes);
        }
    }
}
