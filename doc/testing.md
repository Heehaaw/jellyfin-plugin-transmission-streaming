# Testing and isolation

Tests use xUnit, AutoFixture/AutoMoq, and FluentAssertions. Unit suites use xUnit v2. Integration and application suites use xUnit v3, with shared fixtures for scenarios that
need containers. The integration project groups host wiring, packaging, and service flows by feature. Main CI verifies the complete suite before publishing a retained artifact;
the manual Prepare release job promotes both plugins' DLLs into a draft with shared version metadata and reruns no tests.

See [category rules](testing-guidelines.md#choose-the-category) for deciding where a test belongs. Packaging and installed-metadata file checks live in integration suites.

## Prerequisites

Install the .NET 10 SDK. The container suites also need a running local Docker-compatible daemon. Their first run pulls pinned Jellyfin, Transmission, and .NET SDK images.
The catalog-install case also pulls a pinned Playwright image for headless Chromium.

Assembly fixtures own one Transmission daemon, one C# peer, and their isolated network for each container suite. Each scenario acquires an exclusive lease, uses a fresh directory
beneath `~/.cache/transmission-streaming-tests`, and removes its torrents and peer state before releasing the lease. Share that location with Docker. Containers stop at assembly
teardown rather than after every test.

One application assembly fixture owns the downloader, peer, browser, initialized baseline, and live Jellyfin host. Playback and configuration cases reuse that host while keeping
separate media paths and viewers. Installation cases copy the stopped baseline into a fresh server so they still begin without the plugin. The tests use synthetic media
and generated state.

## Reuse without shared scenario state

Each container-test assembly starts one Transmission daemon, one C# peer/HTTP process, and one isolated network. An exclusive scenario lease gives each test a fresh media and
projection directory. Releasing the lease removes its torrents, cancels old peer connections, resets controlled upstream state, and deletes the scenario data. The next case borrows
the same processes rather than recompiling or restarting them. The controlled HTTP process also serves local plugin repositories.

Import and handoff cases that begin with completed media prepare a file, add its torrent paused, and ask the real daemon to verify it before starting seeding. They do not wait
for another peer transfer. Partial playback and the complete download-to-import case still transfer real pieces. Application fixtures release the first piece only when a case
requests a stream, so configuration checks do not wait for media.

Playback and configuration scenarios reuse one live Jellyfin process. Cleanup disables discovery and waits for `IsPolling` and active readers to drain, removes libraries and
scenario viewers through Jellyfin's APIs, clears plugin projection and completion rows, restores disabled defaults, and verifies the empty state before releasing the scenario.
The [test-only helper](../tests-application/TransmissionStreaming/Jellyfin.ApplicationTestHost/README.md) performs the SQLite reset inside Jellyfin so its WAL and pooled
connections stay coherent. It lives under `tests-application`, is loaded only by the disposable
playback host, and is excluded from both published plugin ZIPs. A failed reset stops the host and prevents further reuse. The assembly fixture stops Jellyfin and the browser before
removing download services, networks, and files. Explicit restart assertions remain part of each scenario that tests persistence.

A stopped, empty server baseline still amortizes migrations for installation cases. Each gets its own copied database and configuration, begins without the plugin, and
loads it through the intended installation path.

## Run the suites

Run unit cases through their `.Tests.` namespace, excluding integration and application cases:

```sh
dotnet test JellyfinPlugins.slnx -c Release --filter 'FullyQualifiedName~.Tests.'
```

This builds the solution but runs only matching unit cases. Container support lives under `tests-integration/`; container suites belong to integration and application projects.

Run the Transmission integration project:

```sh
dotnet test tests-integration/TransmissionStreaming/TransmissionStreaming.IntegrationTests -c Release
```

Run only host wiring and packaging checks without starting containers:

```sh
dotnet test tests-integration/TransmissionStreaming/TransmissionStreaming.IntegrationTests -c Release \
  --filter "FullyQualifiedName~Host|FullyQualifiedName~Packaging"
```

Downloader services start only when a scenario borrows them. Filtered host and packaging checks need no Docker daemon; service flows retain shared containers and isolated data.

Run the Jellyfin application tests:

```sh
dotnet test tests-application/TransmissionStreaming/Jellyfin.Plugin.TransmissionStreaming.ApplicationTests -c Release
```

Run everything:

```sh
dotnet test JellyfinPlugins.slnx -c Release
```

## Business tests

The suites divide responsibilities so failures point to a specific behavior:

| Layer | Behaviors |
| --- | --- |
| Unit | Service-path suggestions, mount confinement, range and piece boundaries, ownership and video selection, import validation, authorization, reader cleanup, virtual deadlines and caches, status, history merging, and host adapters. |
| Plugin integration | Jellyfin-owned settings, DI composition, options snapshots and live reload, installed metadata, and subscription disposal without starting a server. |
| Packaging integration | Tag normalization, generated package metadata, archive contents, missing files, repeat repacking, and preservation of tested DLL bytes. |
| Component integration | Real handlers, HTTP adapters, SQLite, projection files, and Transmission working together across discovery, playback, import through independent arr mounts, and application restart. |
| Jellyfin application | Actual plugin loading and DI, indexing unfinished media, HTTP delivery and video decoding, duplicate reconciliation, per-user history, server restart, and installation. |

Integration scenarios cover competing releases for one title, a removed torrent followed by a late import, completed bytes awaiting import, recovery after an arr outage, and
rejection of samples and ambiguous episode payloads. Orphan scenarios remove the arr title and torrent, then verify that the tracking row and generated files stay absent across
restart. Native handoff waits for host indexing, then retires active state and generated files while retaining media and completion history across restart. An arr outage must retain them until an authoritative library response becomes available. A completed import reports full progress even when its torrent has already disappeared.

Tests use generated inputs and inject concrete subjects with frozen collaborator mocks. Each test states one behavior, with scenario-specific setup kept in its Arrange block.
Small helpers own external setup or protocol details when that makes the flow easier to read; short arrangements remain inline. Records and interfaces are exercised through
mapping, persistence, authorization, and service composition rather than tests that repeat property assignments.

Each behavior-bearing unit gets a happy-path call plus tests for meaningful branches, loops, main failures, and obvious edge cases. Property defaults and metadata declarations
do not need separate assertion-only tests. Integration and application tests establish whether the assembled plugin performs its intended work; coverage percentages are not a target.

The repository installation flow promotes a development bundle to a tag-derived version without compiling it again, then checks Jellyfin's displayed version after restart and
compares every installed DLL with the master bundle. The final ZIP and catalog checksum describe the repacked package.

The playback flow verifies the description activity link while downloading, after import, and across restart, including duplicate-link prevention.

The cancellation application case also verifies that the actual Jellyfin item disappears, its old playback URL returns not found, and restart does not recreate it.

The catalog-install case also opens Jellyfin's dashboard in headless Chromium, follows the plugin sidebar link, and verifies the settings page loads. Testcontainers owns the
browser server on the isolated test network. It starts lazily, reuses its process, and gives each trial a fresh context. It never uses a host browser or personal profile.
The Playwright package and browser image must share a version; the first browser startup installs the matching Node server package inside its container.

Other container application scenarios verify server behavior through HTTP, not the web client's component lifecycle. Local installation also needs a browser check: load saved settings,
discover paths for each service, edit a mapping, rediscover without losing that edit, save, and reload. Jellyfin's bundled component polyfill requires its own dynamic-element
creation conventions; a plain DOM mock cannot establish compatibility.

Service-path application tests call the authenticated discovery endpoints with unsaved connection details, verify discovery leaves saved settings alone, and retain a manual mapping
override after a Jellyfin restart. Playback scenarios use independent Transmission and arr namespaces with deliberately unrelated shared fallback roots. The import, torrent removal,
restart, and viewer-history assertions therefore also verify that the explicit mappings drive the real file handoff.

## Measured runtime

The current nine-case Transmission application suite was measured with warmed images using `dotnet test ... -c Release --no-build`, including fixture setup and teardown:

| Setup | Wall time | Jellyfin containers | Jellyfin starts |
| --- | ---: | ---: | ---: |
| Fresh server per scenario | 219.3 s | 10 | 17 |
| One live playback/configuration host | 171.2 s | 6 | 13 |

The shared host saved 48.1 seconds, or 22%. A separate reuse run took 173.2 seconds. All nine cases passed in both reuse runs, and the seven intentional persistence/install
restarts remained. Installation cases still use fresh servers. These are local warmed-image measurements, not CI timing guarantees.

The same integration suite was measured before and after preparing verified completed downloads for import-focused cases:

| Run | Before | After | Reduction |
| --- | ---: | ---: | ---: |
| Integration, including build and container lifecycle | 94.1 s | 36.8 s | 61% |

An earlier optimized run took 40.1 seconds. All runs pass the same behavioral assertions. Transmission 4.1.3 updates peer interest on a hardcoded ten-second cycle; there is no supported session setting for that interval.
The transfer cases retain that real scheduling behavior. These local Docker measurements include warmed image caches and are not CI guarantees.

Before the additional business scenarios, the same six scenarios were measured before and after assembly reuse, including fixture setup and teardown:

| Suite | Before | After | Reduction |
| --- | ---: | ---: | ---: |
| Application | 158.1 s | 136.1 s | 14% |
| Integration | 72.9 s | 52.7 s | 28% |

These are local Docker measurements, not CI guarantees. Transmission still schedules real peer requests on its own clock. At that point, the unit-only command completed all 78 tests in
3.44 seconds with warmed dependencies, including SDK/build overhead; virtual-time test bodies complete in milliseconds.

The unit-only command completes in a few seconds with warmed dependencies, including build overhead. Additional business scenarios and the decoder check change the workload,
so current totals are separate from the like-for-like reuse comparison above.

## Virtual time

Application policy unit tests use generated in-memory bytes and mocked offset reads. Physical offset reads, missing paths, and cancellation are tested separately against temporary
files in the infrastructure suite. Tests follow the owning feature and layer when a policy moves.

Unit tests inject `TimeProvider` and advance `FakeTimeProvider` rather than sleeping. They check exact discovery intervals, failure and retry timestamps, continuous-stall deadlines,
deadline reset after progress, cancellation, and availability-cache expiry. Duration calculations use monotonic timestamps, so changing wall time cannot extend a stalled request
or keep an expired cache alive.

The observer waits until a timer is registered before advancing it. Its real-time guard detects a broken test synchronization contract; it does not drive the behavior under test.
Container tests use the real clocks of Jellyfin and Transmission and bounded condition polling to observe their progress.

## Service integration flow

The integration suite composes real application handlers, HTTP adapters, filesystem access, and the SQLite store through dependency injection. Jellyfin's indexing boundary is
replaced for these tests, so they can exercise discovery and playback without starting a media server. Each discovery or read entry owns a DI scope, matching the host's operation lifetime.

A real Transmission daemon downloads a private synthetic torrent from a controlled local peer. The tests check verified partial reads, exact import matching, persisted identity
after reopening the application, and playback after torrent removal. Separate scenarios cover an unrelated import, an arr failure, disabled discovery, and Sonarr episode ownership.

## Hosted application flow

The application suite starts a fresh Jellyfin server with the built plugin and synthetic video. A C# file-based app supplies controlled Radarr/Sonarr responses, a local tracker, and
a BitTorrent peer. Transmission transfers and verifies actual torrent bytes. Its piece map is not mocked.

The peer first releases only the first piece, then releases the remaining pieces on command. The torrent is private, with no public trackers, DHT, PEX, or local peer discovery.
This makes missing-piece assertions independent of Internet traffic and public-swarm timing.

The scenarios exercise discovery before completion, HEAD and range responses, waiting for missing bytes without returning sparse zeros, cancellation, and a bounded stall. They
complete the torrent and move the file into the simulated arr library. While it remains in Transmission, the same proxy and Jellyfin item retain each viewer's state. After torrent
removal, the flow waits for native handoff and checks all seven transferred user-data fields, retired capabilities and generated files, bounded history, and retained media. Native
Jellyfin streaming is then checked after disabling the plugin and restarting the server. Both projection and import folders belong to one library configured before completion.
Its initial scan finishes while the import folder is empty, so library creation or a later global scan cannot mask a failed handoff scan. A separate scan-race scenario checks
duplicate reconciliation during seeding.

Container readiness checks and bounded condition polling replace fixed startup sleeps. A restarted container can receive a new host port; the test client rebinds while the
permanent URL inside the test network stays unchanged. Failure diagnostics include sanitized container logs without capability secrets.

Startup prefill is also tested against real Jellyfin libraries during catalog installation. The application test checks inferred paths and confirms that explicit overrides
survive a server restart. Unit tests cover path selection, supplied-value precedence, cancellation, and optional startup failures. The settings script is checked for blank-field
prefill, preserved overrides, reverse-proxy base paths, discarded query parameters, and unavailable suggestions.

## What still needs a client trial

These suites exercise service contracts, HTTP playback, and library state. They do not establish native-player compatibility, transcoding behavior, public-swarm performance, or
behavior under a normal multi-library workload. Test the intended client with a small real download before widening deployment.

## Installation flows

Application tests also exercise installation in fresh servers. The local development case runs the installer, checks the copied Debug assemblies, and loads the plugin. Catalog
cases serve a controlled repository and ZIP, install through Jellyfin's package API, restart, and check that the plugin loads. A mismatched checksum must prevent installation.

After publishing a release, verify the actual public repository with:

```sh
TRANSMISSION_STREAMING_TEST_REPOSITORY="https://github.com/Heehaaw/jellyfin-plugins/releases/latest/download/manifest.json" \
  dotnet test tests-application/TransmissionStreaming/Jellyfin.Plugin.TransmissionStreaming.ApplicationTests -c Release \
  -p:Version=1.2.3 --filter FullyQualifiedName~Should_InstallFromRepositoryAndLoadAfterRestart
```

Replace the example Version with the published package version and use the matching tagged checkout. The property sets the probe's expected package metadata; the installed DLLs
come from the public release. This probe creates a disposable Jellyfin instance and installs the published ZIP. The regular suite uses its controlled
local repository, so CI can verify a new version before that version is released.

[Back to README](../README.md)

## HDR Playback suites

HDR owns separate unit, integration, and application projects beneath their `HdrPlayback` scope folders. They are all included in `JellyfinPlugins.slnx`. The unit-only command
filters cases by their `.Tests.` namespace. Run its player transport checks with `node --test tests/HdrPlayback/Player/player.test.cjs`. See [HDR Playback](hdr-playback.md)
for the focused commands.

The HDR application fixture owns a disposable Jellyfin instance containing both plugins and synthetic SDR media, with a `/jf` base path. Its Chromium trial uses an isolated
container and browser context. These tests do not modify the home server or establish C4 HDR display quality.
