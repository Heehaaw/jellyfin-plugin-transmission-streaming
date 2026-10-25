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
            var encodedKey = Encoding.ASCII.GetBytes(key);
            WriteBytes(encodedKey);
            if (value is int number)
            {
                var encodedNumber = Encoding.ASCII.GetBytes("i" + number + "e");
                result.Write(encodedNumber);
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
            var prefix = Encoding.ASCII.GetBytes(bytes.Length + ":");
            result.Write(prefix);
            result.Write(bytes);
        }
    }
}
