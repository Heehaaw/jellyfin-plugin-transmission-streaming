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

using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using TransmissionStreaming.TestSupport.Torrents.Seed;

// HTTP controls decide which pieces the peer advertises. Transmission still owns
// the transfer, hash verification, disk cache, and incomplete-to-completed move.
var configuration = JsonSerializer.Deserialize<SeedConfiguration>(File.ReadAllText("/fixture/seed.json"),
    new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
var media = File.ReadAllBytes("/fixture/reference.mkv");
var control = new SeedControl(configuration, media);
var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
var app = builder.Build();

using var listener = new TcpListener(IPAddress.Any, configuration.PeerPort);
listener.Start();
using var stopListener = app.Lifetime.ApplicationStopping.Register(listener.Stop);
_ = BitTorrentPeer.AcceptAsync(listener, control, app.Lifetime.ApplicationStopping);
TrackerEndpoints.Map(app, control);
ControlEndpoints.Map(app, control);
ArrEndpoints.Map(app, control);
await app.RunAsync($"http://0.0.0.0:{configuration.HttpPort}");
