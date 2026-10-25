namespace Jellyfin.Plugin.TransmissionStreaming.Http;

using global::TransmissionStreaming.Core.Streaming;
using System.Globalization;

/// <summary>Parses one HTTP byte range into host-neutral inclusive response bounds.</summary>
internal static class HttpByteRange
{
    public static bool TryParse(string? header, long length, out ByteRange range)
    {
        range = new(0, length, false);
        if (string.IsNullOrEmpty(header))
        {
            return length >= 0;
        }

        if (length <= 0 || !header.StartsWith("bytes=", StringComparison.OrdinalIgnoreCase) || header.Contains(','))
        {
            return false;
        }

        var parts = header[6..].Split('-');
        if (parts.Length != 2)
        {
            return false;
        }

        if (parts[0].Length == 0)
        {
            if (!Number(parts[1], out var suffix) || suffix == 0)
            {
                return false;
            }

            var count = Math.Min(suffix, length);
            range = new(length - count, count, true);
            return true;
        }

        if (!Number(parts[0], out var start) || start >= length)
        {
            return false;
        }

        var end = length - 1;
        if (parts[1].Length != 0 && (!Number(parts[1], out end) || end < start))
        {
            return false;
        }

        end = Math.Min(end, length - 1);
        range = new(start, end - start + 1, true);
        return true;
    }

    private static bool Number(string text, out long value)
    {
        return long.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value);
    }
}
