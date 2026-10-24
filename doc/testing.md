# Testing and isolation

Tests use xUnit, AutoFixture/AutoMoq, and FluentAssertions. The unit suites remain on xUnit v2; the container suites use xUnit v3 assembly fixtures. Integration and application tests use Testcontainers for .NET with small fixtures owned by this repository. They have no
SGP-library dependencies. CI runs the same complete suite before packaging an artifact or publishing a tagged release.

## Prerequisites

Install the .NET 10 SDK. The container suites also need a running local Docker-compatible daemon. Their first run pulls pinned Jellyfin, Transmission, and .NET SDK images.

Assembly fixtures own one Transmission daemon, one C# peer, and their isolated network for each container suite. Each scenario acquires an exclusive lease, uses a fresh directory
beneath `~/.cache/transmission-streaming-tests`, and removes its torrents and peer state before releasing the lease. Share that location with Docker. Containers stop at assembly
teardown rather than after every test.

Jellyfin cases retain independent configuration, databases, and media libraries. An empty server is initialized and stopped once; each case copies that baseline before starting
its own server. This skips repeated migrations without sharing mutable databases or carrying watch state between scenarios. The tests use synthetic media and generated state.

## Reuse without shared scenario state

Each container-test assembly starts one Transmission daemon, one C# peer/HTTP process, and one isolated network. An exclusive scenario lease gives each test a fresh media and
projection directory. Releasing the lease removes its torrents, cancels old peer connections, resets controlled upstream state, and deletes the scenario data. The next case borrows
the same processes rather than recompiling or restarting them. The controlled HTTP process also serves local plugin repositories.

Jellyfin remains a separate process for each scenario. A stopped, empty server baseline amortizes database migrations and initial setup. Each scenario gets its own copied database
and configuration; mutable library data and watch history are never shared. Installation tests still begin without the plugin and load it through the intended installation path.

## Run the suites

Run the fast unit and adapter tests without building or discovering the container suites:

```sh
dotnet test tests/UnitTests.slnx -c Release
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

## Measured runtime

The same six scenarios were measured before and after assembly reuse, including fixture setup and teardown:

| Suite | Before | After | Reduction |
| --- | ---: | ---: | ---: |
| Application | 158.1 s | 136.1 s | 14% |
| Integration | 72.9 s | 52.7 s | 28% |

These are local Docker measurements, not CI guarantees. Transmission still schedules real peer requests on its own clock. The unit-only command completed all 78 tests in
3.44 seconds with warmed dependencies, including SDK/build overhead; virtual-time test bodies complete in milliseconds.

## Virtual time

Unit tests inject `TimeProvider` and advance `FakeTimeProvider` rather than sleeping. They check exact discovery intervals, failure and retry timestamps, continuous-stall deadlines,
deadline reset after progress, cancellation, and availability-cache expiry. Duration calculations use monotonic timestamps, so changing wall time cannot extend a stalled request
or keep an expired cache alive.

The observer waits until a timer is registered before advancing it. Its real-time guard detects a broken test synchronization contract; it does not drive the behavior under test.
Container tests use the real clocks of Jellyfin and Transmission and bounded condition polling to observe their progress.

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
