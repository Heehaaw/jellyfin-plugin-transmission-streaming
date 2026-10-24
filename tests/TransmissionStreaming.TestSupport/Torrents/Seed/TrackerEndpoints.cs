namespace TransmissionStreaming.TestSupport.Torrents.Seed;

using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;

internal static class TrackerEndpoints
{
    public static void Map(WebApplication app, SeedControl control)
    {
        app.MapGet("/health", () => Results.Json(new { }));
        app.MapGet("/announce", async (HttpContext context) =>
        {
            var addresses = await Dns.GetHostAddressesAsync(control.Configuration.Host, context.RequestAborted);
            var address = addresses.First(value => value.AddressFamily == AddressFamily.InterNetwork).GetAddressBytes();
            var compact = new byte[6];
            address.CopyTo(compact, 0);
            BinaryPrimitives.WriteUInt16BigEndian(compact.AsSpan(4), (ushort)control.Configuration.PeerPort);
            return Results.Bytes(Bencode.Encode(new SortedDictionary<string, object>(StringComparer.Ordinal)
            {
                ["interval"] = 1, ["min interval"] = 1, ["complete"] = 1, ["incomplete"] = 0, ["peers"] = compact
            }), "text/plain");
        });
    }
}
