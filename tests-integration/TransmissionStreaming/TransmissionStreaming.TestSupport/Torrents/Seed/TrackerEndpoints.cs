namespace TransmissionStreaming.TestSupport.Torrents.Seed;

using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;

internal static class TrackerEndpoints
{
    public static void Map(WebApplication app, SeedControl control)
    {
        app.MapGet(
            pattern: "/health",
            handler: () => Results.Json(new
            {
            })
        );
        app.MapGet(
            pattern: "/announce",
            handler: async (HttpContext context) =>
            {
                var addresses = await Dns.GetHostAddressesAsync(control.Current.Configuration.Host, context.RequestAborted);
                var hostAddress = addresses.First(value => value.AddressFamily == AddressFamily.InterNetwork);
                var address = hostAddress.GetAddressBytes();
                var compact = new byte[6];
                address.CopyTo(compact, 0);
                var port = (ushort)control.Current.Configuration.PeerPort;
                BinaryPrimitives.WriteUInt16BigEndian(compact.AsSpan(4), port);

                var response = new SortedDictionary<string, object>(StringComparer.Ordinal)
                {
                    ["interval"] = 1,
                    ["min interval"] = 1,
                    ["complete"] = 1,
                    ["incomplete"] = 0,
                    ["peers"] = compact
                };
                var bytes = Bencode.Encode(response);
                return Results.Bytes(bytes, "text/plain");
            }
        );
    }
}
