# Installation, development, and releases

There are two installation flows. Development builds load directly into a selected local Jellyfin plugin directory. Published releases install through Jellyfin's normal plugin
repository and Catalog.

## Local development

Install the .NET 10 SDK and clone the repository. Stop the development Jellyfin server, then run from the checkout:

```sh
dotnet run --file scripts/Install.cs -- --plugins-dir "/path/to/jellyfin/plugins"
```

The command builds Debug and copies the plugin's declared assemblies and generated metadata into a versioned directory beneath the chosen root. Add `--configuration Release`
after the CLI separator for a Release build.
It does not control the Jellyfin service. Make the resulting files readable by its service account, then start or restart Jellyfin to load them.

The destination name comes from plugin metadata. Development builds use `Transmission Streaming_0.0.0.0`; published packages use the tag-derived version in the same format.
If you previously installed the same plugin manually under another folder name, keep only one installation of that version before restarting.

After restart, open **Dashboard → Plugins → Transmission Streaming** in the left sidebar to configure the plugin.

Typical plugin roots are `/var/lib/jellyfin/plugins/` for native Linux and `/config/plugins/` inside the Jellyfin container. Choose the path belonging to the server you intend to
load; a path on the development machine does not automatically reach a remote server.

## Build a ZIP

Run:

```sh
dotnet run --file scripts/Package.cs
```

This builds the Release plugin and creates `artifacts/TransmissionStreaming_<version>.zip`. The package contains only the assemblies declared in plugin metadata and `meta.json`,
all at the ZIP root. Build output paths come from MSBuild, so the packaging process follows the selected target framework automatically.

For a direct manual installation, stop Jellyfin, create the metadata-derived version directory under its plugin root, and extract the ZIP there. Ensure the service account can
read it, then restart Jellyfin. Do not copy the whole build output or replace Jellyfin's own assemblies.

## CI artifacts

Pushes to `main`, pull requests targeting `main`, and manual runs on `main` build and run every test suite, including the disposable Transmission and Jellyfin tests. A successful
build uploads the plugin ZIP as a workflow artifact with 90-day retention. Open the completed run under GitHub Actions and download that artifact for manual installation. GitHub
requires sign-in for workflow-artifact downloads.

The artifact is already the plugin ZIP; it is uploaded without another wrapping archive. Normal users should use the published repository installation below.

## Published release and repository flow

```mermaid
flowchart LR
    P[Push main] --> C[Build and all tests]
    C --> T[Tag the passing commit]
    T --> V[Require successful main CI]
    V --> Z[Reuse DLLs or rebuild if absent]
    Z --> P2[Stamp tag metadata and repack]
    P2 --> R[GitHub Release]
    R --> M[Stable repository manifest URL]
    M --> J[Jellyfin Catalog install]
    J --> S[Restart Jellyfin]
```

Push the release commit to `main` and let CI pass, then create a numeric `v` version tag on that same commit. The tag determines the package version; for example, `v1.2.3`
produces `1.2.3.0`. No source-version edit or post-release bump is needed. See [versioning](versioning.md) for development versions and stable assembly identity.

Tag runs require a successful `main` push run for the exact tagged commit. A passing pull-request or manual run cannot substitute for it. The release reuses that run's DLLs,
generates tag-derived metadata, and repacks the ZIP without compilation. If the artifact is missing or expired, it builds and packages the tagged source. Neither path reruns tests.
If the tag arrives before main CI passes, the
release fails immediately; rerun the release job after main CI succeeds.

The release package includes a repository manifest with the plugin identity, version, target Jellyfin ABI, immutable tag-specific ZIP URL, and MD5 checksum of those exact ZIP bytes.
MD5 is the checksum Jellyfin's repository protocol requires. The previous published manifest is merged so older versions remain available. Release workflows run serially to retain
that catalog history, and an already published plugin version cannot be reused.

The workflow creates a draft release, uploads the ZIP and manifest, then publishes it as the latest release. The stable repository address is:

```text
https://github.com/Heehaaw/jellyfin-plugin-transmission-streaming/releases/latest/download/manifest.json
```

The first release makes this URL available. Every subsequent release updates its contents while each version's ZIP URL remains tied to its own tag. If an upload fails and leaves
a draft release, remove that incomplete draft before rerunning the release job.

To install through Jellyfin:

1. Open **Dashboard → Plugins → Manage Repositories**.
2. Add a repository name and the stable manifest URL above.
3. Find **Transmission Streaming** in the Catalog and install a compatible version.
4. Restart Jellyfin and open the plugin settings.

Jellyfin reads the manifest, filters compatible versions, downloads the ZIP, checks its MD5, and extracts it into the version directory. It then requests a restart. The target ABI
identifies the host API the package was built against; client playback still needs validation with the intended setup.

For a manually hosted release, the packaging command also accepts `--source-url` with the final HTTPS ZIP address, ending in `.zip`. It writes `artifacts/manifest.json` alongside
the ZIP. Supply `--previous-manifest-url` to retain an existing repository's versions. Publish the exact ZIP and manifest together; the default packaging command creates no catalog
entry for an unpublished artifact.

See the [official Jellyfin plugin guide](https://jellyfin.org/docs/general/server/plugins/) for its Catalog and manual-install workflow.

[Back to README](../README.md)
