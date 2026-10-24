# TODO

## After the base implementation

- [ ] Add application and integration tests using Testcontainers for .NET. Follow ASCO's separation of hosted application tests and service-level integration tests, with small local fixtures. Do not depend on SGP libraries or introduce a general testing framework.
- [ ] Add an integration test project that composes the real feature bootstrappers, generated Mediator handlers, HTTP adapters, projection store, and filesystem. Use a disposable SQLite file, a real Transmission container, and controlled Radarr/Sonarr HTTP responses. Exercise discovery, exact import matching, persistence across restart, and failure handling through public contracts.
- [ ] Add an application test project that starts a disposable Jellyfin container with the built plugin and drives its HTTP API. Give each scenario fresh configuration, plugin state, library data, and synthetic media. Verify plugin discovery and DI as part of startup.
- [ ] Exercise the download flow with a real Transmission daemon and a private synthetic torrent served by a controlled local seed. Keep peers within the test network, disable public trackers/DHT/PEX, and control piece availability so buffering assertions do not depend on Internet traffic or download timing.
- [ ] Verify discovery before completion, correct HEAD/range responses, waiting for missing pieces without returning zeros, resumption when pieces arrive, cancellation and stall handling. Complete and import the file, remove the torrent, then verify unchanged playback URL and Jellyfin item identity, including after restart.
- [ ] Verify two users retain watched state, resume position, play count, and favorites through completion. Cover a native duplicate appearing during a scan and reconciliation without deleting the media file.
- [ ] Keep fixture ownership explicit with xUnit async lifecycle, dynamic host ports, container readiness checks, bounded condition polling, and automatic cleanup. Capture useful logs on failure without recording credentials or stream secrets. Share container startup only where each test's state can still be reset safely.
- [ ] Document the Docker prerequisite and separate commands for unit, integration, and application tests. Pin tested container versions when implementing the fixtures. Keep the existing host smoke test until its assertions have equivalent automated coverage. Do not add CI.

These are planned tests. The current validation consists of the unit/adapter tests and the isolated Jellyfin host smoke test described in the README.
