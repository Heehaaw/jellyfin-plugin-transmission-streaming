# Configuration and buffering

The plugin starts disabled. Configure connections and paths before enabling discovery.

## Initial defaults and suggestions

On startup, the plugin saves defaults for missing local paths. A blank projection folder becomes `library` beneath the plugin's data directory. For a blank local media root, it
looks at the parents of existing Jellyfin Movies and Shows folders. For example, `/mnt/media/movies` and `/mnt/media/shows` suggest `/mnt/media`.

The plugin excludes its own projection folders, invalid paths, and relative paths. It leaves the local root empty when there is no usable common parent or the result would be a
filesystem root such as `/`. Review any inferred root: it must also contain the incomplete and completed download folders through the shared mapping.

Saved nonblank values always win, and initialization never enables discovery. Prefill is best effort; unavailable library information or a failed defaults lookup must not prevent
Jellyfin from starting. Existing configured installations retain their supplied settings.

When the settings page opens, it asks an administrator-only, read-only defaults endpoint for current suggestions. This allows blank fields to reflect libraries added since startup
without changing saved configuration. A blank Jellyfin URL is suggested from the connected server's address, including any reverse-proxy base path. Credentials, query strings,
and fragments are excluded; addresses containing credentials are not suggested. Check that the resulting URL is reachable by Jellyfin itself and every player.

Suggestions filled into the page persist only when you click **Save**. Transmission and arr addresses, credentials, API keys, and the remote media root still require your input.
The plugin does not scan the network or guess credentials.

## Connections and shared paths

| Setting | What to enter |
| --- | --- |
| Transmission RPC URL | An address Jellyfin can reach, including `/transmission/rpc`, such as `http://download-host:9091/transmission/rpc` |
| Transmission credentials | The existing RPC username and password |
| Radarr and Sonarr | Each application's base URL and API key; configure whichever manages the media you want to expose |
| Media root seen by Transmission and arr | The common remote path, for example `/data` |
| Same media root seen by Jellyfin | The matching local mount, for example `/mnt/media` |
| Dedicated streaming library folder | A new empty folder writable by Jellyfin, for example `/srv/jellyfin-streaming` |
| Jellyfin URL | A URL reachable by both the Jellyfin server and every player, including any reverse-proxy base path |

Root mapping translates paths without moving files. For example, `/data/downloads/incomplete/movie.mkv.part` becomes `/mnt/media/downloads/incomplete/movie.mkv.part`. Jellyfin needs
read access to incomplete downloads, completed downloads, and the final arr libraries. A read-only media mount is sufficient. Its projection folder and plugin data folder must be writable.

Transmission and arr must use the same shared media layout. Independent remote roots or different arr download mappings are outside this prototype's configuration model.
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
download error. Completion removes the suffix while retaining the entry.

The player controls its buffering indicator. There is no custom badge understood by every native Jellyfin client. The plugin settings page provides a separate discovery/status view.

An item can appear once the torrent's file list is known, but playback still needs file headers and enough data to start. Some formats require bytes from the end of the file for
probing. Continued playback requires a swarm fast enough to keep up with the video's bitrate.

## Timing

Discovery defaults to a 10-second interval, configurable from 2 to 300 seconds. A continuous playback stall defaults to a 120-second deadline, configurable from 5 to 600 seconds.
A player's own timeout can expire sooner. Slow or missing peers can still cause playback failure.

[Back to README](../README.md)
