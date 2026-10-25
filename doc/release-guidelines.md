# Release guidelines

Follow the delivery scope authorized in the conversation. A request to install locally does not authorize a push, tag, public release, or remote installation.

## Before publication

Run focused checks while editing and the assembled suite before delivery. Verify the local build through the requested install path, preserve configuration and mappings, and check the actual Jellyfin settings page. Test the intended player separately when playback behavior changes.

Describe pending verification plainly. Passing unit tests or packaging a ZIP does not establish a successful installation or native handoff.

## Published release order

1. Review the complete change and use semantic commits when publication is authorized.
2. Push main and wait for its successful CI run for the exact commit.
3. Run **Actions → Prepare release** on main, selecting that commit and a patch, minor, major, or custom version. The job requires passing main CI for the exact commit.
4. Review the prepared draft in **Releases**. It must contain both plugin ZIPs and the merged repository manifest. Publish it manually as Latest.
5. Verify Catalog installation and restart through a disposable server, then perform any separately authorized remote installation.

Main and pull-request CI run all tests and package both installable ZIPs. Artifacts are retained for 90 days. The manually dispatched preparation job preserves the
successful main artifacts' DLLs, generates shared version metadata, and repacks both ZIPs without rerunning tests. If an artifact is missing or expired, it builds the
selected source before the same promotion step.

Tag pushes never create or publish releases. The preparation job leaves a draft; only a user publishes it. Publish or discard the pending draft before preparing another
release so its catalog includes the latest published history.

The tag is the package-version source of truth. Do not bump release versions in source or mutate the tested DLLs. See [versioning](versioning.md) for the development and promotion flows.

The release manifest must describe the final repacked ZIP, including its version, source URL, and checksum. Preserve catalog history and serialize competing release jobs.

## Installation evidence

Verify the loaded plugin version and active status after restart. Check preserved settings, path translations, and library identity/history, plus browser errors and any behavior changed by the release. Keep tokens and stream capability keys out of diagnostics.

See [installation and releases](installation.md) for commands and [testing](testing.md) for the public repository installation probe.
