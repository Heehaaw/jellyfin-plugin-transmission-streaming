# Completed work

## Playback and completion

- [x] Play local torrents in Jellyfin before their downloads finish.
- [x] Wait for missing data safely, with cancellation and bounded buffering.
- [x] Continue playback after arr imports the downloaded file.
- [x] Preserve each viewer's watched state, resume position, play count, favorites, and ratings.
- [x] Switch to native Jellyfin playback after the torrent is removed.
- [x] Keep imported movies visible in libraries containing both streaming and completed media.
- [x] Remove cancelled downloads from tracking without deleting imported media or interrupting playback.
- [x] Link movie descriptions to download activity and show recent completed handoffs.

## Configuration and settings

- [x] Prefill discoverable settings while preserving existing values.
- [x] Discover Transmission, Radarr, and Sonarr folders with editable path mappings.
- [x] Provide responsive settings with activity, errors, folder checks, and contextual help.
- [x] Preserve saved configuration across plugin updates.

## Installation and distribution

- [x] Build and install locally for development.
- [x] Install published releases through Jellyfin's plugin repository.
- [x] Retain verified build artifacts for release and recover when artifacts expire.
- [x] Publish releases without repeating the tests already passed on main.

## Verification and documentation

- [x] Verify downloading, import, cleanup, viewer history, native playback, and restart behavior automatically.
- [x] Verify repository installation and reject damaged packages.
- [x] Keep test runs repeatable and efficient.
- [x] Document installation, configuration, design, operation, and contributor expectations.
