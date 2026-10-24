# Design and playback flow

The plugin gives an incomplete torrent a permanent Jellyfin library entry. Its small stream link stays in place while the media moves from an incomplete download to the final
arr library. Transmission owns downloading and seeding; Jellyfin owns playback and each viewer's history.

```mermaid
flowchart LR
    A[Radarr or Sonarr] -->|starts download| T[Transmission]
    T -->|file list and verified pieces| P[Jellyfin plugin]
    P -->|permanent stream entry| J[Jellyfin library and players]
    A -->|imports completed file| F[Local media library]
    F -->|same entry, imported backing file| P
```

## Configuration initialization

Startup fills only missing local settings: a projection folder beneath the plugin's data directory and, where possible, a shared media root inferred from existing Movies and Shows
library paths. It ignores invalid or relative paths and its own projection folders, and rejects a filesystem root as too broad. Unclear media roots stay empty. Saved nonblank
settings retain ownership, and discovery stays disabled until the administrator enables it. An optional prefill failure must not block server startup.

The settings page can request fresh local suggestions through an administrator-only, read-only endpoint after libraries change. It also suggests the connected Jellyfin address for
a blank public URL, retaining a reverse-proxy base path without credentials, query strings, or fragments. These page suggestions are saved only on **Save**. External-service
addresses and secrets remain explicit configuration.

Per-service buttons read remote directories through the configured APIs, then propose editable translations into Jellyfin's filesystem. Discovery is read-only; the administrator
saves the reviewed mappings. Each service has an independent namespace, with the old shared-root pair retained as a fallback. See [configuration](configuration.md#connections-and-service-paths)
for examples and checks.

The plugin keeps Jellyfin's XML configuration as its persisted source. A host adapter configures standard .NET options and translates configuration-save events into change
tokens. Singleton handlers read the current monitored options, while a previously obtained snapshot retains its own mapping values. Core and Logic do not depend on Jellyfin
packages or configuration APIs.

## Discovery

The plugin polls Transmission and matches each playable file to a configured arr queue entry by torrent hash. A movie download uses its primary video file. Standard single-episode
filenames identify episodes within a season pack. Unmanaged downloads, samples, and ambiguous identities are skipped.

Discovery creates a permanent stream link and metadata sidecars beneath a dedicated projection folder. Jellyfin scans these as ordinary movies and episodes. Persisted state keeps
the entry's identity, capability secret, original torrent file, and eventual imported path together across restarts.

## Progressive playback

Pressing Play opens the permanent stream URL. The reader checks Transmission's verified piece map before reading each byte range from the shared filesystem. An allocated file may
already have its final length while much of its content is still missing, so file length alone never proves that a range is playable.

The reader sends available bytes and waits for missing pieces. It does not substitute zeros for missing data. By default, starting playback enables sequential downloading for that
torrent and moves its sequential cursor toward missing playback pieces. See [write-cache safety](configuration.md#sequential-downloading-and-write-cache-safety) for the disk-read requirement.

Cancellation stops the copy and releases its reader lease. A continuous stall has a deadline; if playback has already sent bytes, the connection is aborted instead of appending an
error document to the video. HEAD requests report length without starting a torrent read.

## Completion

After arr imports the download, the plugin checks the import history against the torrent hash, original filename, byte size, and imported file identity. A matching title or an
older library copy is insufficient proof. Once the exact import is confirmed, the same stream URL reads the imported local file, including after Transmission removes the torrent.

The permanent Jellyfin entry remains the owner of watch history. Native duplicate detection and rollback are described in [operations](operations.md).

## Application boundaries

The domain defines media identities, playback state, and external-service contracts. Application requests coordinate discovery, authorization, streaming, and completion through
source-generated Mediator handlers. Root inference, service-mapping precedence, verified-piece gating, prioritization, and stall deadlines belong to application policy.\nInfrastructure supplies HTTP clients, physical path confinement, offset reads, and SQLite persistence. Copying reads through a filesystem contract so the application tests its\nwaiting and cancellation rules without disk access. The plugin composes these layers
and adapts them to Jellyfin's configuration, HTTP endpoints, indexing, and user metadata.

Each layer uses feature folders. Infrastructure and the plugin compose their adapters through feature bootstrappers. Logic contains Mediator queries and commands; generated
registration discovers their internal handlers without per-feature bootstrappers. Service implementations remain internal behind Core contracts. Jellyfin's public entry points
are the framework-required exceptions. The installed plugin needs no separate server, torrent engine, or database service.

Playback commands carry media identity, output, range, and read options. Each copy creates data-only session state for its cached torrent metadata, timestamps, and imported path.
Handlers receive the client, store, filesystem, options, clock, and Mediator through DI. Runtime messages do not carry service instances, service factories, or callbacks.

The settings UI uses focused browser modules for draft configuration, folder mappings, activity, and contextual help. Jellyfin serves the embedded page, stylesheet, and modules
from the plugin; there is no separate frontend server or build pipeline. Each page instance owns its draft and polling lifecycle. Tooltips explain the controls, and faint dashed
underlines mark explanatory text with help. Buttons keep their normal cursor and plain text. Hover or keyboard focus shows help after 150 ms; leaving the control, scrolling, or interacting dismisses it. Responsive layout uses the same field and action-button heights throughout.

## Resource use

The plugin reads shared files and calls the Transmission, Radarr, and Sonarr APIs. It does not contact torrent peers or download a second media copy. Existing VPN isolation and
seeding rules remain with Transmission.

Each active copy rents a 128 KiB buffer, plus runtime, request, and metadata overhead. Readers check availability about every half second. Discovery defaults to every 10 seconds
and can be configured from 2 to 300 seconds. Import checks follow the polling cadence rather than running for every output chunk.

## Prototype limits

- Only downloads owned by a configured Radarr or Sonarr queue are discovered. Manually added torrents without an arr identity are skipped.
- Standard single-episode `S01E02` and `1x02` filenames are supported. Ambiguous multi-episode files and absolute-numbered anime names are not resolved.
- Transmission has one sequential cursor per torrent. Viewers at different positions can compete, so seeking is best effort.
- There is one stable projection per movie or episode. A later upgrade does not take it over automatically.
- Import matching rejects ambiguous history, different file sizes, and unrelated older releases.
- Permanent stream links continue to route through the plugin after completion. Removing it requires a library migration or restoring a backup.

[Back to README](../README.md)
