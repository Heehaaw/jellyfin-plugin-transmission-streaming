# TODO

## Container flow tests

- [x] Add application and integration tests with Testcontainers for .NET and small local fixtures, without SGP libraries.
- [x] Compose real application handlers, HTTP adapters, projection storage, and filesystem in integration tests. Exercise discovery, exact import matching, persisted state, disabled discovery, and upstream failures with real Transmission.
- [x] Start disposable Jellyfin instances with the built plugin and fresh configuration, library state, and synthetic video. Verify plugin discovery and dependency injection through its API.
- [x] Transfer a private synthetic torrent through a controlled C# peer and real Transmission. Release pieces explicitly, with public trackers, DHT, PEX, and local peer discovery disabled.
- [x] Check partial playback, HEAD and ranges, missing-piece waits, cancellation, stalls, completion, import, torrent removal, stable playback URLs, and server restart.
- [x] Retain two users' watched state, resume positions, play counts, and favorites. Reconcile a native duplicate without deleting its media file.
- [x] Use explicit async ownership, dynamic host ports, readiness checks, bounded polling, automatic cleanup, and sanitized failure logs.
- [x] Document test isolation and separate commands in `doc/`.

## Installation and distribution

- [x] Verify local build-and-load and ZIP packaging commands.
- [x] Verify installation through Jellyfin's Catalog API, including checksum rejection.
- [x] Run CI, publish the first tagged release, and verify the stable repository manifest and ZIP download.

## Deterministic and faster tests

- [x] Inject `TimeProvider` into discovery, streaming deadlines, and availability caches.
- [x] Advance virtual time in unit tests for poll boundaries, retries, expiry, cancellation, and stall reset.
- [x] Reuse download containers and networks through assembly fixtures while resetting scenario state.
- [x] Measure complete-suite runtime after reuse and rerun Linux CI.
- [x] Publish and verify the release containing the timer and fixture improvements.

## Business tests and testability

- [x] Exercise each behavior-bearing unit on its happy path and test meaningful branches, loops, main failures, and obvious edges with AutoFixture.
- [x] Add component scenarios for competing releases, late import recovery, completed downloads awaiting import, and rejected payloads.
- [x] Verify progressive video decoding against the complete original in the Jellyfin application flow.
- [x] Audit test readability, run the expanded suites, and publish the verified result.

## Initial configuration prefill

- [x] Fill missing projection and local media roots from host-local information, preserving supplied values and leaving discovery disabled.
- [x] Suggest the connected Jellyfin URL and refresh blank path fields through a read-only administrator endpoint; persist page suggestions only on Save.
- [x] Verify happy paths, path exclusions, ambiguous roots, supplied-value precedence, and best-effort startup failure handling.
- [x] Exercise initialization and settings suggestions in the real Jellyfin application flow, then verify the published package.

## Release artifact reuse

- [x] Run builds and tests on main and pull requests, retaining CI ZIPs for 90 days.
- [x] Publish the successful main run's ZIP for the tagged commit, rebuilding that commit only if the artifact is missing or expired, without rerunning tests.
- [x] Verify reuse with a real CI ZIP and verify the source-build fallback without running tests.
- [ ] Confirm the revised tag job on the next versioned release.

## Service path discovery

- [x] Discover Transmission download directories and each arr service's library roots using the form's connection details.
- [x] Offer editable mappings per service, retaining manual entries and the existing shared fallback.
- [x] Confine download and imported paths to their service mappings, with stable projection identity across the handoff.
- [x] Run all 231 tests and verify local 0.1.3 in the existing Jellyfin LXC: five service mappings discovered, local folders readable, old settings and library identities preserved.
- [x] Verify the real Jellyfin web page: load saved connections, query all services, retain manual edits on rediscovery, and save/reload the five mappings.
- [x] Group mapping pairs, place discovery controls beside URLs, and expose runtime metrics, folder-check results, and unsaved/save state; verify desktop and mobile browser behavior.
- [x] Move mapping and playback rules with their tests into Logic, compose inference/copying through Mediator, and keep physical reads in Infrastructure.
- [x] Install and verify the redesigned settings page and 240-test build locally before the further DI pass.
- [x] Replace mapping/video/playback services with CQ handlers, remove service-bearing playback factories, and remove redundant Logic bootstrappers and DI packages.
- [x] Complete 253 server tests, options live-reload verification, redundant-child filtering, and local installation with preserved configuration.
- [x] Split UI assets into focused modules under Ui, add underlined help, and align every input/action height; verify native module loading and resource wiring.
- [x] Verify the final split UI in the installed local Jellyfin, including 150 ms tooltips, faint label hints, plain button cursors/text, uniform control heights, and preserved settings.
- [x] Move real options-monitor and DI-composition checks from unit tests into a separate in-process plugin integration project.
- [ ] Publish the locally verified build after main CI passes, then reinstall through the Jellyfin repository.
