# Package versions and artifact promotion

One shared release tag chooses both plugins' user-visible version. A `v1.2.3` release contains Transmission Streaming and HDR Playback version `1.2.3.0`.
Missing numeric components become zero; prerelease labels are not used for Jellyfin package versions. There is no release-version edit or next-version bump on main.

## Development builds

The project defaults to `0.0.0-dev`. A normal build generates adjacent plugin metadata with the numeric development version `0.0.0.0`. The checked-in metadata is a template;
building does not replace it or modify the project file. The SDK records the development version and source revision in the assembly's informational version.

The plugin assembly keeps the stable binary identity `1.0.0.0`. This identity and the installed package version have different purposes. At startup, the plugin reads its
adjacent package metadata so Jellyfin's plugin API and dashboard report the installed package version. A DLL-only manual installation falls back to the assembly version.
Invalid metadata prevents activation instead of displaying an invented version.

## Release promotion

Main CI builds, tests, and retains both development ZIPs. After the branch commit passes, manually run **Actions → Prepare release**, selecting that branch. Choose
`patch`, `minor`, or `major`
to increment the highest published stable version, or choose `custom` and supply a `vMAJOR.MINOR.PATCH` tag. The job downloads both artifacts and passes the shared
version to metadata generation.
That step evaluates the project without compiling. Repacking replaces `meta.json`, names the release ZIP from the package version, and preserves every DLL's contents.
The catalog checksum describes each final repacked ZIP, so release ZIPs are expected to differ from development ZIPs. Both packages and their retained histories are
merged into one catalog in the draft release. Publish that draft manually in Releases; tag pushes do not trigger publication.

The job selects successful main CI for the exact branch commit and downloads its retained ZIPs. Missing or expired artifacts stop preparation; plugin DLLs are never silently
rebuilt. No version-bump commit follows publication. The run summary links to the actual draft URL, which can be untagged until it is published.

To exercise promotion locally:

```sh
dotnet run --file scripts/Package.cs -- --archive /path/to/master.zip --release-tag v1.2.3
```

This creates `artifacts/TransmissionStreaming_1.2.3.0.zip` without rebuilding its assemblies. Omit `--archive` to exercise the fallback build. Repository-manifest options apply to
the resulting package; its version, source URL, and checksum must describe the final ZIP.

The application installation tests promote a development bundle to another package version, install it through Jellyfin, restart the server, and check the reported version and
unchanged DLL hashes. See [installation and releases](installation.md) for commands and [release guidelines](release-guidelines.md) for delivery order.

For HDR Playback, select its artifact explicitly:

```sh
dotnet run --file scripts/Package.cs -- --plugin hdr-playback --archive /path/to/hdr-playback.zip --release-tag v1.2.3
```

This creates `artifacts/HDRPlayback_1.2.3.0.zip`. The preparation job promotes both plugins to the same version and preserves each plugin's existing catalog history. Preparation
jobs run serially; publish or discard the pending draft before preparing another version.
