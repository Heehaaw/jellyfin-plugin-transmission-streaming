# Transmission Streaming for Jellyfin

A prototype Jellyfin plugin that makes Radarr- and Sonarr-managed Transmission downloads playable before they finish. Playback stays in Jellyfin, including watched status and
resume positions. Once arr imports the completed download, the same library entry reads the imported file.

Targets Jellyfin 12.3, .NET 10, and Transmission 4.1.3. Forward playback is the priority; seeking is best effort. Native clients, transcoding, and public-swarm performance still
need a trial with the intended player.

## Install a published release

Back up Jellyfin's database and configuration before the first trial. Start with one small test download and a dedicated projection folder.

1. Open **Dashboard → Plugins → Manage Repositories** and add this repository URL:

   ```text
   https://github.com/Heehaaw/jellyfin-plugin-transmission-streaming/releases/latest/download/manifest.json
   ```

2. Open the Catalog, select **Transmission Streaming**, and install a compatible version.
3. Restart Jellyfin, then open **Dashboard → Plugins → Transmission Streaming → Settings**.

See [installation and releases](doc/installation.md) for downloading a ZIP directly or using a CI artifact.

## Build and load for development

Install the .NET 10 SDK and clone this repository. With your development Jellyfin server stopped, run:

```sh
dotnet run --file scripts/Install.cs --plugins-dir "/path/to/jellyfin/plugins"
```

This builds Debug and copies the complete plugin bundle into the chosen plugin directory. Add `--configuration Release` for a Release build. Make sure the Jellyfin service account
can read the resulting directory, then start Jellyfin. Typical plugin roots are `/var/lib/jellyfin/plugins/` on native Linux and `/config/plugins/` in a container.

To compile a distributable Release ZIP without installing it:

```sh
dotnet run --file scripts/Package.cs
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
dotnet test TransmissionStreaming.slnx -c Release
```

Main-branch CI and pull requests run these suites and retain an installable ZIP for 90 days. After main CI passes, a version tag publishes its ZIP and updates the Jellyfin
repository manifest without rerunning tests. Missing or expired artifacts are rebuilt from the tagged commit. See [testing](doc/testing.md)
for separate test commands and coverage boundaries, and [installation and releases](doc/installation.md) for the two installation flows.

## Further reading

- [Installation, development, and releases](doc/installation.md)
- [Design and playback flow](doc/design.md)
- [Configuration and buffering](doc/configuration.md)
- [Completion, watch history, and rollback](doc/operations.md)
- [Testing and isolation](doc/testing.md)
- [TODO list](TODO.md)

## License

MIT. Jellyfin and the other dependencies retain their own licenses.
