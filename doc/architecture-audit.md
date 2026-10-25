# Architecture audit

Reviewed on 2026-10-25 during native-handoff implementation. This audit distinguishes application policy from adapter work and records the remaining boundary debt.

## Established boundaries

Core has no package dependencies. Logic references Core, Mediator abstractions, and framework options; it has no Jellyfin or Infrastructure dependency. Infrastructure references Core. Plugin composes those layers and owns Jellyfin/HTTP integration.

Playback progression, prioritization, service-path decisions, resolver visibility, reconciliation, per-user history merging, native handoff, and completion-history retention now live in Logic use cases. Playback state and requests carry data. The adapter store takes a retention limit rather than choosing one.

The thin Jellyfin library adapter obtains canonical item facts, playback activity, and hydrated user snapshots, writes mapped metadata/history, and removes database entries without deleting media. HTTP range responses, cancellation-to-response handling, live options bridging, and Jellyfin link formatting are legitimate host concerns.

## Remaining boundary debt

| Area | Remaining application decision | Suggested boundary |
| --- | --- | --- |
| Arr download discovery and filename interpretation | Chooses supported files, rejects samples and ambiguous episode payloads, and decides ownership from queue candidates | Let Infrastructure return typed queue/episode facts. Keep conservative selection and ambiguity decisions in Logic. |
| Arr import resolution | Accepts an import using hash, identity, basename, size, history, and linked episode cardinality | Keep endpoint access and JSON translation in Infrastructure; move acceptance of those facts into Logic. |
| Projection paths | One adapter combines identity keys, stable hashed IDs, media naming/layout, and physical remote-to-local confinement | Keep physical confinement in Infrastructure. Move stable identity and generated layout rules into application/domain policy without changing existing IDs or paths. |
| Core byte range | A useful range value also parses HTTP header syntax | Keep the range data host-neutral and place HTTP parsing at the HTTP boundary. This is dependency-direction debt, not a new security finding. |

These moves should preserve existing conservative rejection and safety checks. They do not require a general rules engine or wrapper service around Mediator.

## Adapter responsibilities to retain

- Transmission RPC/session mapping, arr endpoint pagination/JSON validation, and service-path enumeration belong with their external protocols.
- Physical file access, verified-length checks, symlink refusal, generated-file ownership checks, and transactional persistence belong in Infrastructure.
- Jellyfin canonical lookup, hydrated user-data access, sessions, scans, and metadata-only deletion belong in Plugin.
- Native UI resource loading and controller lifecycle belong in Plugin. The UI must not duplicate media acceptance or handoff policy.

Unknown identities, malformed library responses, and outages must continue to retain active state. Moving policy must not broaden destructive cleanup or weaken import matching.

## Verification boundary

The assembled unit, component, and real-Jellyfin suites verify these boundaries through native handoff, user-history transfer, cleanup, restart, and playback with the plugin disabled. Local LXC installation also preserves configuration, library identities, and watch data. Intended-player and public-swarm trials remain separate from server/API verification.
