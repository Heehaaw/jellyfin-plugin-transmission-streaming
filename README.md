# Jellyfin plugins

Independent plugins for Jellyfin 12.3 and .NET 10, maintained in one repository.

| Plugin | Purpose |
| --- | --- |
| Transmission Streaming | Play arr-managed Transmission downloads before completion, retaining native library identity and watch history. |
| [HDR Playback](doc/hdr-playback.md) | Opt-in SDR-to-HDR conversion in the native transcode, selected in the player settings menu. AMD VA-API/Vulkan on Linux. |

Both install independently from the same catalog. A manually prepared release contains both plugins at the same version. Renaming the repository does not change the
Transmission plugin GUID, assemblies, configuration, or routes.

Source and tests are scoped by plugin:

```text
src/{TransmissionStreaming,HdrPlayback}/
tests/{TransmissionStreaming,HdrPlayback}/
tests-integration/{TransmissionStreaming,HdrPlayback}/
tests-application/{TransmissionStreaming,HdrPlayback}/
```

Each plugin owns Core, Logic, Infrastructure, and Jellyfin adapter projects. Shared build scripts and CI package them separately.

## Transmission Streaming

A prototype Jellyfin plugin that makes Radarr- and Sonarr-managed Transmission downloads playable before they finish. Playback stays in Jellyfin, including watched status and
resume positions. After import, the stream reads the completed file while Transmission keeps seeding. Once the torrent is removed, the plugin transfers each viewer's history to the
native Jellyfin item and retires its proxy.

Targets Jellyfin 12.3, .NET 10, and Transmission 4.1.3. Forward playback is the priority; seeking is best effort. Native clients, transcoding, and public-swarm performance still
need a trial with the intended player.

## Install a published release

Back up Jellyfin's database and configuration before the first trial. Start with one small test download and a dedicated projection folder.

1. Open **Dashboard → Plugins → Manage Repositories** and add this repository URL:

   ```text
   https://github.com/Heehaaw/jellyfin-plugins/releases/latest/download/manifest.json
   ```

2. Open the Catalog, select **Transmission Streaming**, and install a compatible version.
3. Restart Jellyfin, then open **Dashboard → Plugins → Transmission Streaming → Settings**.

See [installation and releases](doc/installation.md) for downloading a ZIP directly or using a CI artifact.

## Build and load for development

Install the .NET 10 SDK and clone this repository. With your development Jellyfin server stopped, run:

```sh
dotnet run --file scripts/Install.cs -- --plugin transmission-streaming --plugins-dir "/path/to/jellyfin/plugins"
```

This builds Debug and copies the complete plugin bundle into the chosen plugin directory. Add `--configuration Release` for a Release build. Make sure the Jellyfin service account
can read the resulting directory, then start Jellyfin. Typical plugin roots are `/var/lib/jellyfin/plugins/` on native Linux and `/config/plugins/` in a container.

To compile a distributable Release ZIP without installing it:

```sh
dotnet run --file scripts/Package.cs -- --plugin transmission-streaming
```

The ZIP is written beneath `artifacts/`. Use the matching Jellyfin version; the package reuses the SQLite runtime supplied by Jellyfin.

## First download

The plugin starts disabled. It fills missing local paths from its data directory and existing Movies/Shows libraries where possible. The settings page also suggests the connected
Jellyfin address. Enter the Transmission and arr connections, then use Discover folders beside each service URL. Review its editable service-folder → Jellyfin-folder pairs and
save them with a Jellyfin URL reachable by both the server and its players.

Jellyfin needs read access to incomplete downloads, completed downloads, and imported media. Its plugin data and dedicated projection folders must be writable.

Enable discovery, then add the projection's `movies` folder to a Movies library and its `shows` folder to a Shows library. Start an arr-managed download, wait for torrent metadata
and the next discovery poll, and play the new Jellyfin item. The settings page reports discovery and download status.

See [configuration](doc/configuration.md) for the settings, path examples, and sequential-download requirements.

## Tests and CI

With a local Docker-compatible daemon running:

```sh
dotnet test JellyfinPlugins.slnx -c Release
```

Main-branch CI and pull requests run these suites and retain a separate installable ZIP for each plugin for 90 days. After main CI passes, run **Actions → Prepare release**.
Choose the branch and a patch, minor, major, or custom version. Its selected commit must have passing main CI. The job prepares a draft with both plugin ZIPs and the
merged catalog. Review it in **Releases**, then publish
it as Latest. Pushing a tag alone does not publish a release. The job reuses both tested ZIPs and links directly to the prepared draft. Missing or expired artifacts stop
preparation. See [versioning](doc/versioning.md) and
[testing](doc/testing.md)
for separate test commands and coverage boundaries, and [installation and releases](doc/installation.md) for the two installation flows.

## Further reading

- [Installation, development, and releases](doc/installation.md)
- [Design and playback flow](doc/design.md)
- [Configuration and buffering](doc/configuration.md)
- [Completion, watch history, and rollback](doc/operations.md)
- [Testing and isolation](doc/testing.md)
- [Architecture rules](doc/architecture.md)
- [C# readability and layout](doc/csharp-style.md)
- [Architecture audit](doc/architecture-audit.md)
- [Testing guidelines](doc/testing-guidelines.md)
- [UI guidelines](doc/ui-guidelines.md)
- [Release guidelines](doc/release-guidelines.md)
- [Transmission Streaming TODO](doc/transmission-streaming-todo.md)

## License

MIT. Jellyfin and the other dependencies retain their own licenses.
