#!/usr/bin/env dotnet
#:sdk Microsoft.NET.Sdk.Web
#:property PublishAot=false
#:include SeedControl.cs
#:include TrackerEndpoints.cs
#:include ControlEndpoints.cs
#:include ArrEndpoints.cs
#:include Bencode.cs
#:include BitTorrentPeer.cs
#:include BitTorrentProtocol.cs
#:include BitTorrentTransfer.cs
#:include CatalogEndpoints.cs

using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using TransmissionStreaming.TestSupport.Torrents.Seed;

// HTTP controls decide which pieces the peer advertises. Transmission still owns
// the transfer, hash verification, disk cache, and incomplete-to-completed move.
var json = File.ReadAllText("/fixture/seed.json");
var options = new JsonSerializerOptions
{
    PropertyNameCaseInsensitive = true
};
var configuration = JsonSerializer.Deserialize<SeedConfiguration>
(
    json: json,
    options: options
)!;
var media = File.ReadAllBytes("/fixture/reference.mkv");
using var control = new SeedControl
(
    configuration: configuration,
    media: media
);
var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
var app = builder.Build();

using var listener = new TcpListener
(
    localaddr: IPAddress.Any,
    port: configuration.PeerPort
);
listener.Start();
using var stopListener = app.Lifetime.ApplicationStopping.Register(listener.Stop);
_ = BitTorrentPeer.AcceptAsync
(
    listener: listener,
    control: control,
    cancellationToken: app.Lifetime.ApplicationStopping
);
TrackerEndpoints.Map
(
    app: app,
    control: control
);
ControlEndpoints.Map
(
    app: app,
    control: control
);
ArrEndpoints.Map
(
    app: app,
    owner: control
);
CatalogEndpoints.Map
(
    app: app,
    control: control
);
await app.RunAsync($"http://0.0.0.0:{configuration.HttpPort}");
