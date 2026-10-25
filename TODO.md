# Completed features

## Playback and completion

- [x] Play unfinished local torrents through ordinary Jellyfin movie and episode entries.
- [x] Buffer safely when requested data is unavailable, with cancellation and bounded waits.
- [x] Support partial playback and byte-range requests.
- [x] Keep playback links stable through download completion, import, torrent removal, and restart.
- [x] Match imports to the downloaded release rather than an older library copy.
- [x] Preserve each viewer's watched state, resume position, play count, favorites, ratings, and likes.
- [x] Reconcile duplicate library entries without deleting media.
- [x] Keep completed downloads available through their streaming entry while seeding.
- [x] Switch to native Jellyfin playback after torrent removal.
- [x] Retain pending handoffs while playback, indexing, or history transfer needs more time.
- [x] Show recent completed handoffs in a collapsible history.
- [x] Keep imported movies visible when streaming and completed folders share one library.
- [x] Support imports into initially empty library folders.
- [x] Preserve native playback and viewer history after the plugin is disabled.

## Cancellation and activity

- [x] Distinguish cancelled titles from downloads awaiting import.
- [x] Remove cancelled titles from tracking and Jellyfin without deleting imported media.
- [x] Preserve entries during active playback and service outages.
- [x] Keep cleanup reliable across server restarts.
- [x] Link movie descriptions to download activity without replacing the synopsis.
- [x] Support activity links behind a reverse proxy without duplicating links.

## Configuration and settings

- [x] Prefill discoverable settings while preserving supplied values.
- [x] Keep initial discovery disabled and handle ambiguous folders conservatively.
- [x] Discover Transmission download folders and Radarr and Sonarr library folders.
- [x] Offer editable per-service path mappings and retain the shared fallback.
- [x] Keep downloads and imports within their configured service folders.
- [x] Remove redundant discovered mappings while retaining manual overrides.
- [x] Preserve unsaved edits during discovery and save changes only on request.
- [x] Group mapping pairs and place discovery actions beside connection fields.
- [x] Show download activity, errors, folder-check results, and save state.
- [x] Fit settings to desktop and narrow screens without horizontal overflow.
- [x] Explain settings through unobtrusive hover hints.
- [x] Align form controls and keep action buttons consistent.
- [x] Preserve configuration through reloads and plugin updates.

## Installation and maintenance

- [x] Build and install locally for development.
- [x] Install published packages through Jellyfin's repository and Catalog.
- [x] Reject damaged installation packages.
- [x] Provide a stable plugin-repository address.
- [x] Retain verified build artifacts and recover when they expire.
- [x] Reuse verified artifacts without repeating release tests.
- [x] Derive the installed plugin version from release tags.
- [x] Repack verified artifacts without changing their assemblies.
- [x] Run lightweight integration checks without starting containers.
- [x] Preserve configuration, libraries, and viewer history during updates.
- [x] Verify playback, import, cleanup, history, installation, and restart behavior automatically.
- [x] Keep automated checks repeatable and efficient.
- [x] Document setup, operation, design, and contributor expectations.
