# Completion, watch history, and rollback

## Keep one Jellyfin identity

The projection's path and Jellyfin entry remain stable when arr imports the completed file. Only the backing path changes. The stream URL continues working after Transmission
removes the torrent, provided the exact imported file remains available at its recorded path.

While the plugin manages a completed projection, Jellyfin's resolver hides the native file to avoid a second library entry. A scan may index that file before import reconciliation.
In that case, the plugin merges every user's state into the projection before removing the duplicate database entry. It does not delete the media file. An active session on the
native item is allowed to finish before reconciliation removes that entry.

Watch-state merging keeps meaningful playback progress and watched status, retains favorites, and preserves the greater play count. The projection remains the entry viewers use
after completion. This handoff does not require keeping two media copies.

## Back up the complete identity

Back up Jellyfin's database and configuration, the plugin's persisted state, and its projection folder together. The plugin database contains stable stream identities, per-entry
secrets, and backing paths. Regenerating projection files alone cannot restore the existing Jellyfin identity or its history.

Use a small test download and a dedicated projection folder for the first trial. Restore a matching set of files if rolling back that trial.

## Stream links and credentials

A stream URL is a capability URL. Possessing its secret permits reading that entry while the plugin is enabled. Treat generated links, sidecars that contain them, and plugin state
as private. Use a private network or HTTPS for remote playback. Jellyfin and player logs may contain stream URLs, so keep those logs private too.

Transmission and arr credentials remain in local Jellyfin plugin settings. They are not bundled into the repository or generated stream URLs.

## Disable or remove the plugin

Disabling the plugin stops its stream endpoints and stops hiding managed native files. A later Jellyfin scan can expose completed physical media normally. Watch history is not
automatically migrated from projections back to native entries.

For an exact rollback after a trial, stop Jellyfin and restore its pre-trial database, configuration, plugin state, and projection folder together. Keep the projection folder and
its database if you intend to resume the existing projected library. Removing the plugin permanently requires a library migration or restoring the pre-trial backup.

[Back to README](../README.md)
