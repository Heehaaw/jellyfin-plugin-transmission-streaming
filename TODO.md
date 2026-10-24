# TODO

## Container flow tests

- [x] Add application and integration tests with Testcontainers for .NET and small local fixtures, without SGP libraries.
- [x] Compose real application handlers, HTTP adapters, projection storage, and filesystem in integration tests. Exercise discovery, exact import matching, persisted state, disabled discovery, and upstream failures with real Transmission.
- [x] Start disposable Jellyfin instances with the built plugin and fresh configuration, library state, and synthetic video. Verify plugin discovery and dependency injection through its API.
- [x] Transfer a private synthetic torrent through a controlled C# peer and real Transmission. Release pieces explicitly, with public trackers, DHT, PEX, and local peer discovery disabled.
- [x] Check partial playback, HEAD and ranges, missing-piece waits, cancellation, stalls, completion, import, torrent removal, stable playback URLs, and server restart.
- [x] Retain two users' watched state, resume positions, play counts, and favorites. Reconcile a native duplicate without deleting its media file.
- [x] Use explicit async ownership, dynamic host ports, readiness checks, bounded polling, automatic cleanup, and sanitized failure logs.
- [x] Replace the Python smoke test with C# application coverage. Document test isolation and separate commands in `doc/`.

## Installation and distribution

- [x] Verify local build-and-load and ZIP packaging commands.
- [x] Verify installation through Jellyfin's Catalog API, including checksum rejection.
- [x] Run CI, publish the first tagged release, and verify the stable repository manifest and ZIP download.

## Deterministic and faster tests

- [x] Inject `TimeProvider` into discovery, streaming deadlines, and availability caches.
- [x] Advance virtual time in unit tests for poll boundaries, retries, expiry, cancellation, and stall reset.
- [x] Reuse download containers and networks through assembly fixtures while resetting scenario state.
- [ ] Measure complete-suite runtime after reuse and rerun Linux CI.
- [ ] Publish and verify the release containing the timer and fixture improvements.
