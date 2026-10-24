# Testing and isolation

Tests use xUnit, AutoFixture/AutoMoq, and FluentAssertions. Integration and application tests use Testcontainers for .NET with small fixtures owned by this repository. They have no
SGP-library dependencies. CI runs the same complete suite before packaging an artifact or publishing a tagged release.

## Prerequisites

Install the .NET 10 SDK. The container suites also need a running local Docker-compatible daemon. Their first run pulls pinned Jellyfin, Transmission, and .NET SDK images.

Fixtures bind a unique directory beneath `~/.cache/transmission-streaming-tests`; share that location with Docker. Each fixture owns its containers, network, and data directory and
removes them on disposal. The tests use synthetic media and generated state. They do not reuse the home stack's settings, credentials, or media.

## Run the suites

Run the fast unit and adapter tests without Docker:

```sh
dotnet test TransmissionStreaming.slnx -c Release --filter 'FullyQualifiedName!~IntegrationTests&FullyQualifiedName!~ApplicationTests'
```

Run the service integration tests:

```sh
dotnet test tests-integration/TransmissionStreaming.IntegrationTests -c Release
```

Run the Jellyfin application tests:

```sh
dotnet test tests-application/Jellyfin.Plugin.TransmissionStreaming.ApplicationTests -c Release
```

Run everything:

```sh
dotnet test TransmissionStreaming.slnx -c Release
```

## Service integration flow

The integration suite composes real application handlers, HTTP adapters, filesystem access, and the SQLite store through dependency injection. Jellyfin's indexing boundary is
replaced for these tests, so they can exercise discovery and playback without starting a media server.

A real Transmission daemon downloads a private synthetic torrent from a controlled local peer. The tests check verified partial reads, exact import matching, persisted identity
after reopening the application, and playback after torrent removal. Separate scenarios cover an unrelated import, an arr failure, disabled discovery, and Sonarr episode ownership.

## Hosted application flow

The application suite starts a fresh Jellyfin server with the built plugin and synthetic video. A C# file-based app supplies controlled Radarr/Sonarr responses, a local tracker, and
a BitTorrent peer. Transmission transfers and verifies actual torrent bytes. Its piece map is not mocked.

The peer first releases only the first piece, then releases the remaining pieces on command. The torrent is private, with no public trackers, DHT, PEX, or local peer discovery.
This makes missing-piece assertions independent of Internet traffic and public-swarm timing.

The scenarios exercise discovery before completion, HEAD and range responses, waiting for missing bytes without returning sparse zeros, cancellation, and a bounded stall. They
complete the torrent, move the file into the simulated arr library, remove the torrent, and restart Jellyfin. They then check the permanent playback URL, item identity, and two
viewers' watched status, resume positions, play counts, and favorites. A separate scan-race scenario checks native duplicate reconciliation without deleting imported media.

Container readiness checks and bounded condition polling replace fixed startup sleeps. A restarted container can receive a new host port; the test client rebinds while the
permanent URL inside the test network stays unchanged. Failure diagnostics include sanitized container logs without capability secrets.

## What still needs a client trial

These suites exercise service contracts, HTTP playback, and library state. They do not establish native-player compatibility, transcoding behavior, public-swarm performance, or
behavior under a normal multi-library workload. Test the intended client with a small real download before widening deployment.


## Installation flows

Application tests also exercise installation in fresh servers. The local development case runs the installer, checks the copied Debug assemblies, and loads the plugin. Catalog
cases serve a controlled repository and ZIP, install through Jellyfin's package API, restart, and check that the plugin loads. A mismatched checksum must prevent installation.

After publishing a release, verify the actual public repository with:

```sh
TRANSMISSION_STREAMING_TEST_REPOSITORY="https://github.com/Heehaaw/jellyfin-plugin-transmission-streaming/releases/latest/download/manifest.json" \
  dotnet test tests-application/Jellyfin.Plugin.TransmissionStreaming.ApplicationTests -c Release \
  --filter FullyQualifiedName~Should_InstallFromRepositoryAndLoadAfterRestart
```

Use a checkout whose plugin version matches the release. This probe creates a disposable Jellyfin instance and installs the published ZIP. The regular suite uses its controlled
local repository, so CI can verify a new version before that version is released.

[Back to README](../README.md)
