# Testing guidelines

Test behavior in the layer that owns it. When policy moves, move its tests too. Keep adapter tests about protocol, filesystem, or Jellyfin mapping behavior.

## Choose the category

Classify a test by the boundary it exercises, not its speed, fixture size, or whether it needs Docker.

| Category | Location | Responsibility | Examples |
| --- | --- | --- | --- |
| Unit | `tests/` | One unit's decisions with generated inputs and controlled collaborators. | Branches, loops, selection, validation, history merging, fake-time deadlines. |
| Integration | `tests-integration/` | Real components or external boundaries working together. Containers are optional. | Package generation and repacking, real files and SQLite, HTTP adapters with services, DI scopes, options reload. |
| Application | `tests-application/` | The built plugin running inside an actual Jellyfin server, observed through supported APIs. | Installation, startup, discovery, playback, imports, native handoff, viewer history, restart. |

Keep one integration project with feature folders for host wiring, packaging, and service flows. Start shared containers only when a scenario needs them.

Packaging belongs to integration testing. Check generated metadata, archive contents, checksums, SDK behavior, and retained DLL bytes there. Loading the resulting package into
Jellyfin belongs to application testing. A temporary file or an in-process framework does not make a check a unit test.

Keep unit checks focused on meaningful decisions. Use integration and application checks to establish that the assembled behavior works. Put each test beside its owning feature,
and move it when the responsibility moves. Browser and real-player trials complement these categories; HTTP assertions cannot prove client compatibility.

## Unit tests

- Use `[Theory, AutoMoqData]`, a generated input, and the concrete subject. Put frozen collaborator mocks before the subject parameter.
- Keep Arrange, Act, and Assert visible. Customize AutoFixture only for type-wide validity or recursion constraints; arrange a branch inside its test.
- Assert generated values or named arranged values. Do not construct replacement source graphs, duplicate arbitrary literals, or verify incidental calls.
- Cover the happy path, meaningful decisions and loops, main failures, and obvious edges. Records and contracts need behavioral use, not getter tests.
- Use small nearby helpers when they clarify repeated setup. Do not build a general test framework for a few scenarios.
- Inject TimeProvider and advance fake time. Synchronize timer registration before advancing; never drive unit behavior with sleeps.

## Integration and application tests

In-process integration checks framework options reload and DI composition. Service integration composes real handlers, HTTP adapters, SQLite, and files with a controlled Transmission daemon. Application tests load the plugin into actual Jellyfin and exercise its supported APIs.

Reuse the existing assembly-owned containers and exclusive scenario leases. Each scenario gets independent files, configuration, database state, torrents, and peer state. Do not share viewers' watch history between tests. Keep the torrent private and disable public trackers, DHT, PEX, and local peer discovery.

Use C# file-based apps for controlled external services. Keep images pinned, readiness and condition polling bounded, diagnostics sanitized, and cleanup reliable after partial startup or cancellation. Do not introduce Python, SGP test libraries, or external release dependencies into normal tests.

Verify saved playback history, native handoff, import identity, restart persistence, and failure retention through observable behavior. A passing HTTP playback test does not prove the intended player's behavior.

## UI and delivery checks

Run the actual Jellyfin frontend for settings changes. Plain DOM tests cannot prove compatibility with its component polyfill. Check unsaved edits, per-service discovery, save/reload, desktop/mobile sizing, and errors without exposing credentials.

Use focused checks while editing, then run the assembled suites before delivery. Coordinate shared builds to avoid output races. State which checks passed and which real-client or deployment checks remain pending.

See [testing and isolation](testing.md) for commands, fixture details, and current coverage boundaries.
