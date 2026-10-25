# Application test host helper

This project is a test-only controller library used to reset the Transmission plugin's state while the disposable Jellyfin server keeps running. It is not a separate
user-installable plugin and has no independent plugin identity or configuration. All helper code stays under `tests-application`.

## Why the reset runs inside Jellyfin

The production plugin owns pooled SQLite connections. A reset attempted through another SQLite process on the macOS side of the Docker VM did not become visible
to the live Jellyfin process: the fixture still read its completed-history row after the host-side delete. Running the transaction through Jellyfin's own SQLite runtime keeps
its connections and view of the database coherent. No database file is deleted, replaced, or copied over while the server runs.

Jellyfin libraries and viewers have additional host state and caches, so the fixture removes those through Jellyfin's normal APIs. This helper handles only the plugin's
`projections` and `completed_projections` rows.

## How the DLL reaches the server

1. The application-test project references this project for build ordering with `ReferenceOutputAssembly="false"`. The root solution also contains the helper project.
2. Its `CopyPluginBundle` target copies `Jellyfin.ApplicationTestHost.dll` into `TestHost` beside the application-test runner, separately from the unchanged production bundle.
3. For the shared playback host, `JellyfinContainer.PrepareConfiguration` installs the production bundle into its disposable configuration directory, then copies the helper DLL
   into that plugin directory and appends its name to that copy of `meta.json`'s `assemblies` list.
4. Jellyfin's normal plugin loader loads the whitelisted DLL in the Transmission plugin's assembly context. This matters: its Core types must have the same identity as the
   production services registered in DI. A separate plugin load context would not share those service types automatically.
5. Jellyfin discovers `FixtureController` through its normal MVC application-part registration. The fixture calls it using its authenticated administrator client.

The catalog-install cases start without this helper and still install the original package through the supported installation flow. The production projects never reference
this project. Packaging reads their original metadata and declared assemblies, so neither published ZIP contains the helper DLL or reset endpoint.

## Reset lifecycle

`JellyfinHost.ResetAsync` runs while the scenario still holds the downloader's exclusive lease:

1. Disable discovery through the normal plugin configuration API.
2. Wait for `IsPolling` to become false and existing playback reader counts to reach zero.
3. Remove scenario libraries and viewers through Jellyfin's APIs, waiting for library cleanup to finish. The infrastructure administrator remains available for the next case;
   each playback scenario uses a fresh viewer so watch history does not carry over.
4. Call `DELETE /ApplicationTestHost/state`. The controller requires administrator authorization and rechecks that discovery is disabled and idle. It opens the existing plugin
   database with `ReadWrite` mode and deletes both tables' rows in one transaction. Schema, backing media, and Jellyfin's own database remain intact.
5. Restore disabled default configuration and verify that libraries, scenario viewers, active projections, and completion history are empty before releasing the lease.

A failed reset stops the shared host and prevents later scenarios from borrowing contaminated state. Normal scenario boundaries keep Jellyfin alive. Tests that explicitly
verify restart persistence still restart the real server. The owning assembly fixture disposes Jellyfin and the browser before removing the downloader, peer, network, and files.
