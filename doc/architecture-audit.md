# Architecture audit

Reviewed on 2026-10-25 after the test-purpose and layer-ownership cleanup. This audit records the current boundaries.

## Established boundaries

Core has no package dependencies. Logic references Core, Mediator, and framework options/DI abstractions; it has no Jellyfin or Infrastructure dependency. Infrastructure references Core. Logic owns its Mediator generation and registrations through its root Bootstrapper. Plugin composes the layer roots and owns Jellyfin/HTTP integration.

Playback progression, prioritization, service-path decisions, resolver visibility, reconciliation, per-user history merging, native handoff, and completion-history retention now live in Logic use cases. Playback state and requests carry data. The adapter store takes a retention limit rather than choosing one.

The thin Jellyfin library adapter obtains canonical item facts, playback activity, and hydrated user snapshots, writes mapped metadata/history, and removes database entries without deleting media. HTTP range responses, cancellation-to-response handling, live options bridging, and Jellyfin link formatting are legitimate host concerns.

## Resolved boundary debt

| Area | Current owner |
| --- | --- |
| Arr discovery | `FindArrDownload` and the shared filename policy in Logic select supported files and reject ambiguous ownership. Infrastructure returns typed queue, media and episode facts. |
| Arr imports and tracking | Logic Mediator queries accept import evidence and determine tracked ownership. Infrastructure maps history, current files and validated library responses. |
| Projection identity and layout | `BuildProjectionPaths` in Logic preserves existing hashed IDs and names. Physical confinement remains in Infrastructure; the unused second mapper was removed. |
| Byte ranges | Core retains the interval value. `HttpByteRange` parses HTTP syntax beside the controller. |
| HDR availability | `SelectHdrMode` owns eligibility for both preplay choices and playback. A missing preplay profile is explicit; playback still checks the negotiated profile and transcoding permission. |
| Installed package version | One shared Infrastructure reader replaces the identical filesystem/JSON implementations in both Plugin projects. |

The moved acceptance checks retain conservative rejection, file-length proof, ambiguous-file refusal and failure propagation. No application wrapper services were added.

## Test ownership

Real SQLite, filesystem, DI composition and combined arr protocol/decision checks live under `tests-integration/`. Pure selection, naming and playback decisions remain under
`tests/`. HTTP range parsing tests follow the HTTP adapter. The shared metadata reader has one integration suite under `tests-integration/Shared`.

Tests for the unused mapper and its forwarding-only duplicate were removed. Symlink refusal remains covered against the mapper used in production. Policy unit tests,
protocol integration checks and actual Jellyfin/browser regressions cover different boundaries and remain separate.

## Adapter responsibilities to retain

- Transmission RPC/session mapping, arr endpoint pagination/JSON validation, and service-path enumeration belong with their external protocols.
- Physical file access, verified-length checks, symlink refusal, generated-file ownership checks, and transactional persistence belong in Infrastructure.
- Jellyfin canonical lookup, hydrated user-data access, sessions, scans, and metadata-only deletion belong in Plugin.
- Native UI resource loading and controller lifecycle belong in Plugin. The UI must not duplicate media acceptance or handoff policy.

Unknown identities, malformed library responses, and outages must continue to retain active state. Moving policy must not broaden destructive cleanup or weaken import matching.

## Verification boundary

Unit, component and actual Jellyfin suites exercise handoff, history transfer, cleanup, restart, packaging and playback. Server/API and headless browser checks do not establish
Android decoder compatibility or HDR display quality on a physical television. These structural changes do not imply installation on the home server.
