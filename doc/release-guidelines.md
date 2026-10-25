# Release guidelines

Follow the delivery scope authorized in the conversation. A request to install locally does not authorize a push, tag, public release, or remote installation.

## Before publication

Run focused checks while editing and the assembled suite before delivery. Verify the local build through the requested install path, preserve configuration and mappings, and check the actual Jellyfin settings page. Test the intended player separately when playback behavior changes.

Describe pending verification plainly. Passing unit tests or packaging a ZIP does not establish a successful installation or native handoff.

## Published release order

1. Review the complete change and use semantic commits when publication is authorized.
2. Push main and wait for its successful CI run for the exact commit.
3. Tag that verified commit. Do not use a tag to bypass failing or missing main CI.
4. Let the tag job publish the plugin ZIP and repository manifest.
5. Verify Catalog installation and restart through a disposable server, then perform any separately authorized remote installation.

Main and pull-request CI run all tests and package an installable ZIP. Artifacts are retained for 90 days. The tag job preserves the successful main artifact's DLLs, generates tag-derived metadata, and repacks the ZIP without rerunning tests. If the artifact is missing or expired, it builds the tagged source before the same promotion step.

The tag is the package-version source of truth. Do not bump release versions in source or mutate the tested DLLs. See [versioning](versioning.md) for the development and promotion flows.

The release manifest must describe the final repacked ZIP, including its version, source URL, and checksum. Preserve catalog history and serialize competing release jobs.

## Installation evidence

Verify the loaded plugin version and active status after restart. Check preserved settings, path translations, and library identity/history, plus browser errors and any behavior changed by the release. Keep tokens and stream capability keys out of diagnostics.

See [installation and releases](installation.md) for commands and [testing](testing.md) for the public repository installation probe.
