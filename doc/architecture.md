# Architecture rules

Application policy belongs in Logic. An adapter obtains facts or performs an operation; it does not decide which media to own, when to retire it, or how to merge watch history.

| Layer | Owns | Keep out |
| --- | --- | --- |
| Core | Host-neutral contracts, domain data, and shared value types | Jellyfin types, HTTP framework types, DI setup, external I/O |
| Logic | Ownership, selection, playback progression, import acceptance, reconciliation, handoff, and cleanup decisions | Jellyfin packages, concrete adapters, direct filesystem or network calls |
| Infrastructure | Transmission/arr protocol mapping, SQLite, physical file operations, and path confinement | Cross-service sequencing and product rules |
| Plugin | Jellyfin facts and I/O, HTTP translation, lifecycle hooks, settings bridge, UI assets, and composition | History merge algorithms and media lifecycle policy |

Dependencies point toward Core. Plugin composes Logic and Infrastructure. Logic calls Core contracts and other use cases through Mediator. Infrastructure implements Core contracts without referencing Logic.

## Shared code

`src/Shared/Shared.Infrastructure` contains the duplicated filesystem/JSON package-version reader and its registration. Both plugin bundles include that assembly.
Its interface stays beside the adapter because only plugin composition consumes it. There is no shared application policy requiring a Shared.Core or Shared.Logic project.

Both plugin projects import `src/Shared/PluginPackaging.targets` for identical metadata generation. Plugin-specific Jellyfin hooks, UI, configuration and client capability
mapping remain with their plugin. Similar structure alone is not a reason to share an abstraction.

## Use cases and composition

Use a public static container with a nested public Query or Command and an internal Handler. Queries return facts or calculated results; commands perform work. Keep the request and handler in the matching feature folder, in a `Name.Query.cs` or `Name.Command.cs` file.

Inject interfaces for external behavior. DI creates services; code creates data records. Per-playback state may cache facts and timestamps, but it must not capture services or delegates. Avoid factories whose only purpose is passing a service graph into a request-specific object.

Each layer owns a root Bootstrapper. Logic registers its generated Mediator, handlers, and any other application services. Plugin composes that root with Infrastructure and the host adapters. Hosts and test fixtures call the same Logic entry point, without duplicating its registrations. Feature bootstrappers are needed only for explicit registrations beyond generated handlers. Small internal interfaces can live beside their implementation; shared Core contracts remain separate.

## State and failure boundaries

Use scoped Mediator and handler registrations. HTTP controllers inherit the host request scope. Background polls, startup initialization, and synchronous resolver hooks own explicit scopes and dispose them after their work. Singleton adapters must not retain scoped handlers.

Use `IOptionsMonitor` for current settings. Rebuild independent snapshots after a Jellyfin save, including copied mapping rows. Do not mutate old snapshots.

Use monotonic TimeProvider timestamps for durations, cache expiry, and stalls. Use UTC wall time for recorded events. Propagate cancellation and bound waits for external processes.

History transfer must succeed before deleting the source host entry. Retire active state only after native handoff and generated-file cleanup succeed. Retrying must be safe. Keep history retention policy in Logic and pass the limit to persistence.

Treat physical confinement, symlink refusal, ownership markers, and metadata-only host deletion as adapter safety guarantees. Preserve state when an upstream response cannot prove absence.

See the [current audit](architecture-audit.md) for remaining boundary debt and [design](design.md) for the playback flow.
