# Configuration and buffering

The plugin starts disabled. Configure connections and paths before enabling discovery.

## Initial defaults and suggestions

On startup, the plugin saves defaults for missing local paths. A blank projection folder becomes `library` beneath the plugin's data directory. For a blank local media root, it
looks at the parents of existing Jellyfin Movies and Shows folders. For example, `/mnt/media/movies` and `/mnt/media/shows` suggest `/mnt/media`.

The plugin excludes its own projection folders, invalid paths, and relative paths. It leaves the local root empty when there is no usable common parent or the result would be a
filesystem root such as `/`. This root is a fallback for existing shared-mount setups; individual services can use their own mappings.

Saved nonblank values always win, and initialization never enables discovery. Prefill is best effort; unavailable library information or a failed defaults lookup must not prevent
Jellyfin from starting. Existing configured installations retain their supplied settings.

When the settings page opens, it asks an administrator-only, read-only defaults endpoint for current suggestions. This allows blank fields to reflect libraries added since startup
without changing saved configuration. A blank Jellyfin URL is suggested from the connected server's address, including any reverse-proxy base path. Credentials, query strings,
and fragments are excluded; addresses containing credentials are not suggested. Check that the resulting URL is reachable by Jellyfin itself and every player.

Suggestions filled into the page persist only when you click **Save**. Transmission and arr addresses, credentials, and API keys still require your input.
The plugin does not scan the network or guess credentials.

## Connections and service paths

| Setting | What to enter |
| --- | --- |
| Transmission RPC URL | An address Jellyfin can reach, including `/transmission/rpc`, such as `http://download-host:9091/transmission/rpc` |
| Transmission credentials | The existing RPC username and password |
| Radarr and Sonarr | Each application's base URL and API key; configure whichever manages the media you want to expose |
| Shared fallback mapping | Optional remote and local roots, used only for a service with no explicit path mappings |
| Dedicated streaming library folder | A new empty folder writable by Jellyfin, for example `/srv/jellyfin-streaming` |
| Jellyfin URL | A URL reachable by both the Jellyfin server and every player, including any reverse-proxy base path |

Root mapping translates paths without moving files. For example, `/data/downloads/incomplete/movie.mkv.part` becomes `/mnt/media/downloads/incomplete/movie.mkv.part`. Jellyfin needs
read access to incomplete downloads, completed downloads, and the final arr libraries. A read-only media mount is sufficient. Its projection folder and plugin data folder must be writable.

Click **Discover paths** beside each configured service. Discovery uses the connection fields currently entered in the form, so there is no need to save credentials first:

- Transmission reports its default download directory, directories used by current torrents, and its enabled incomplete directory.
- Radarr and Sonarr each report their own configured library roots. Their mount paths can differ from Transmission and from each other.

Each returned remote folder gets an editable local mapping. Existing administrator mappings take precedence. Otherwise, the plugin uses the shared fallback where it applies,
looks for a unique matching Jellyfin library folder name, or pairs a lone movie/show root with the corresponding lone Jellyfin library folder. It excludes its own streaming
folders. An uncertain local path stays empty for you to fill in. A service cannot report how the same disk is mounted inside Jellyfin, so check these suggestions.

Suggested mappings omit a nested folder when its parent already produces the same local path and permits that translation. For example, a completed-download root covers its
`tv-sonarr` subfolder without a second row. A child with a different local target or a required narrower filesystem boundary remains separate.

You can edit either side, add a mapping manually, or remove a row. Rediscovery preserves rows already in the form, including unsaved overrides, and adds newly found roots.
The purpose labels come from service metadata: the configured default directory, enabled incomplete directory, and per-torrent download directories. They are refreshed by
Discover folders; a saved Transmission row initially says Mapped folder until checked in that page visit.
Nothing changes on the server until **Save**. Saved settings refresh live through the options monitor; no Jellyfin restart is needed for ordinary configuration edits. Discovery does not move files, change download settings, or enable the plugin.

Explicit rows replace the shared fallback for that service. For example, Transmission can map `/downloads` to `/mnt/media/downloads`, Radarr can map `/films` to
`/mnt/media/movies`, and Sonarr can map `/tv` to `/mnt/media/series`. Include every active and incomplete download location you want to stream. Existing configurations without
explicit rows continue using the saved shared fallback.

When roots overlap, the most specific remote root wins. Conflicting translations for that same root are refused. Imported files must remain within their arr service's mapped
local roots, including after a restart or torrent removal. Paths containing traversal or symlinks below a mapped root are refused.

Connection URLs must identify the service directly, without redirects to a login page or another host.

## Library setup

After saving and enabling discovery:

1. The plugin creates `movies` and `shows` beneath the projection folder.
2. Add `movies` to a Jellyfin Movies library and `shows` to a Shows library. They can be additional folders in your existing libraries.
3. Start a download through Radarr or Sonarr. Wait for its torrent metadata and the next discovery poll.
4. Check the plugin settings page for status, then open the new item in Jellyfin and press Play.

You can keep the default projection folder or choose an explicit dedicated folder that is easier to locate and back up. On first use, the plugin refuses a nonempty folder so it
cannot adopt unrelated contents. Keep the projection folder and its persisted state together when backing up or moving the installation.

## Sequential downloading and write-cache safety

Keep **Enable sequential download and prioritize missing playback pieces** enabled for the normal setup. Transmission 4.1.3 flushes completed pieces to the filesystem before
publishing their availability in sequential mode. Otherwise, a verified piece may still exist only in Transmission's application write cache.

If playback prioritization is disabled, the torrent must already use sequential downloading or Transmission's application write cache must be disabled. The reader refuses
progressive playback when neither condition holds. The plugin does not change the global cache setting.

## Status and buffering

With status display enabled, downloading items receive a temporary title suffix such as `[Downloading 23%]`. Other states include waiting for import, an unavailable download, and a
download error. Completion removes the suffix while retaining the entry. The item description includes a **View download status** link to the plugin activity page. This preserves
the synopsis and remains available after import; opening the activity page requires a Jellyfin administrator account.

The standard Jellyfin Movies grid uses cached lists and batched library notifications. New items and updated title suffixes may take longer to appear there; it is not a live
download dashboard. The plugin activity page polls its own status independently.

If you cancel a download and remove its movie or series from arr, the plugin removes its unfinished streaming entry after the next successful scan. Removing only the torrent
keeps the entry available for a late import. Existing imported media and active playback prevent cleanup. Connection errors never count as deletion; cleanup removes only the
plugin-generated link and metadata, without deleting downloaded or imported media files.

The settings page groups each remote folder and its Jellyfin folder into one translation row. Discover folders sits beside the service URL; Add mapping and Remove affect
translations only. Unsaved edits are marked explicitly, and Save reports success or failure without discarding the draft. Credentials stay collapsed until needed.

Live activity reports the saved download-scan state, last successful scan, item count, per-item download progress, and active HTTP readers. It refreshes every 10 seconds while
the page is active, with a manual Refresh button. Reader counts describe streaming requests, not distinct viewers. Folder checks have their own timestamp and result count; they
are administrator-triggered checks, not continuous service-health monitoring. Empty or failed status responses do not imply that every service is healthy.

The player controls its buffering indicator. There is no custom badge understood by every native Jellyfin client. The plugin settings page provides a separate discovery/status view.

An item can appear once the torrent's file list is known, but playback still needs file headers and enough data to start. Some formats require bytes from the end of the file for
probing. Continued playback requires a swarm fast enough to keep up with the video's bitrate.

## Timing

Discovery defaults to a 10-second interval, configurable from 2 to 300 seconds. A continuous playback stall defaults to a 120-second deadline, configurable from 5 to 600 seconds.
A player's own timeout can expire sooner. Slow or missing peers can still cause playback failure.

[Back to README](../README.md)
