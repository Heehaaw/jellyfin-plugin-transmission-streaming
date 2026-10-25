# Completion, watch history, and rollback

## Download, import, and native handoff

The projection's Jellyfin entry and stream URL stay stable while downloading, after arr imports the completed file, and while its torrent remains in Transmission. Import changes only
the backing path. The plugin does not remove torrents or change seeding limits.

While the plugin manages that projection, the resolver hides an unindexed native duplicate. If a scan indexes the physical file first, reconciliation merges every user's state into
the projection before removing the duplicate database entry. It never deletes the media. Playback on the native duplicate defers that removal.

After Transmission removes the torrent, discovery revalidates the exact imported file. Its folder must be part of a normal Jellyfin library. The plugin persists a pending handoff
before asking Jellyfin to index the native file. It waits if that item is not yet available, an HTTP reader is active, or either item is playing. The existing stream remains usable.

Once idle and indexed, the plugin merges every user's history into the native item, retains the description's activity link, and removes only its projected database entry and owned
stream/metadata files. It then atomically retires active tracking and records completion. An interrupted operation retries from durable state. Imported media is retained throughout.

The native item has its own Jellyfin ID and uses normal Jellyfin playback. It no longer needs the proxy or plugin. Old projection links and saved item bookmarks are not redirected.
The settings page removes the active row and keeps the newest 20 successful handoffs in a collapsed **Completed** list with links to their native items. History stores no stream
capabilities or media paths, and is not required for playback.

## Preserve each viewer's history

Playback fields come from the state with the newer last-played timestamp. An untouched destination accepts source activity. If both have activity and dates cannot establish order,
the destination wins, preserving manual changes. The greater play count is retained without double counting; favorites are combined. Existing destination ratings and likes win,
with source values filling missing ones. Every user save must succeed before the source entry is removed.

## Back up the complete identity

Back up Jellyfin's database and configuration, plugin state, and projection folder together. Active state contains stable stream IDs, capability secrets, and backing paths.
Regenerating projection files alone cannot restore an active Jellyfin entry or its history. Use a small test download and a dedicated folder for an initial trial.

## Stream links and credentials

A stream URL is a capability URL. Its secret permits reading an active entry while the plugin is enabled. Treat generated links, sidecars, plugin state, and player/server logs as
private. Use HTTPS or a private network for remote playback. Transmission and arr credentials remain in local plugin settings and are never added to stream URLs.

## Disable, remove, or roll back

Successfully handed-off native items keep working after disabling or removing the plugin. Active projections still depend on it. Disabling stops its endpoints and resolver hiding;
a later scan may expose physical media, but disabling does not perform a watch-history transfer. Let eligible handoffs complete first if retiring the plugin.

For an exact trial rollback, stop Jellyfin and restore its matching pre-trial database, configuration, plugin state, and projection folder. Keep state and generated files together if
resuming managed projections. A backup predating a handoff restores that earlier ownership state.

[Back to README](../README.md)
