# Package versions and artifact promotion

Release tags choose the user-visible plugin version. A tag such as `v1.2.3` becomes Jellyfin version `1.2.3.0`. Missing numeric components become zero; prerelease labels are not
used for Jellyfin package versions. There is no release-version edit or next-version bump on main.

## Development builds

The project defaults to `0.0.0-dev`. A normal build generates adjacent plugin metadata with the numeric development version `0.0.0.0`. The checked-in metadata is a template;
building does not replace it or modify the project file. The SDK records the development version and source revision in the assembly's informational version.

The plugin assembly keeps the stable binary identity `1.0.0.0`. This identity and the installed package version have different purposes. At startup, the plugin reads its
adjacent package metadata so Jellyfin's plugin API and dashboard report the installed package version. A DLL-only manual installation falls back to the assembly version.
Invalid metadata prevents activation instead of displaying an invented version.

## Release promotion

Main CI builds, tests, and retains the development ZIP. After that exact commit passes, the tag job downloads its artifact and passes the tag version to metadata generation.
That step evaluates the project without compiling. Repacking replaces `meta.json`, names the release ZIP from the package version, and preserves every DLL's contents.
The catalog checksum describes the final repacked ZIP, so the release ZIP itself is expected to differ from the development ZIP.

If the retained artifact is missing or expired, the job builds the tagged source and applies the same metadata and packaging steps. Neither release path reruns tests.
No version-bump commit follows publication.

To exercise promotion locally:

```sh
dotnet run --file scripts/Package.cs -- --archive /path/to/master.zip --release-tag v1.2.3
```

This creates `artifacts/TransmissionStreaming_1.2.3.0.zip` without rebuilding its assemblies. Omit `--archive` to exercise the fallback build. Repository-manifest options apply to
the resulting package; its version, source URL, and checksum must describe the final ZIP.

The application installation tests promote a development bundle to another package version, install it through Jellyfin, restart the server, and check the reported version and
unchanged DLL hashes. See [installation and releases](installation.md) for commands and [release guidelines](release-guidelines.md) for delivery order.
