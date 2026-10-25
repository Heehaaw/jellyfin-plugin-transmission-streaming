# Repository rules

Read the relevant linked guide before changing that area. Keep changes focused and preserve existing configuration, media, and watch history.

## Architecture

- Put application decisions and sequencing in Logic Mediator handlers. See [architecture](doc/architecture.md).
- Keep Core host-neutral. Core and Logic must not reference Jellyfin packages or Infrastructure.
- Keep Plugin code to Jellyfin/HTTP/lifecycle adapters, settings integration, UI resources, and the composition root.
- Keep Infrastructure to external protocols, persistence, and filesystem operations. Do not hide application policy in an adapter.
- Inject contracts through DI. Requests and state records carry data, never services, delegates, or factories.
- Use a public static use-case container, a nested public Query or Command, and a nested internal Handler. Name files `Name.Query.cs` or `Name.Command.cs`.
- Default implementations to internal. Expose interfaces only where callers need a boundary. A small implementation-local interface can share its class file.
- Give each layer one root Bootstrapper. Logic owns Mediator and its service graph; the Plugin composes layer roots. Add feature bootstrappers only when a feature needs explicit registrations.
- Use records and modern C# where useful, central package versions, semantic feature folders, and the repository editorconfig. Avoid partial classes.
- Use scoped Mediator handlers. HTTP uses the host request scope; singleton hooks and workers create and dispose a scope per entry. Never capture scoped services in singletons.
- Follow [C# style](doc/csharp-style.md): multiline braces, short methods, guard returns, descriptive names, nearby declarations, and useful helper ordering.
- Chopped calls use one argument per line and names only with multiple supplied arguments, except required optional-parameter binding. Public results use semantic records, never tuples.
- Add brief comments for ownership, timing, retries, concurrency, and other non-obvious intent. Document public contracts.
- Check the [architecture audit](doc/architecture-audit.md) before extending a known mixed responsibility.

## Tests

- Follow [testing guidelines](doc/testing-guidelines.md) and run checks proportionate to the change.
- Test a meaningful happy path, branches, loops, main failures, and obvious edges. Do not chase coverage percentages or repeat getters.
- Tests follow moved behavior into its owning layer. Keep test bodies short and legible.
- Use injected AutoFixture inputs, frozen collaborator mocks, and a concrete subject. No manually assembled source graphs.
- Use TimeProvider and virtual time for unit deadlines and cache expiry. External process checks need bounded polling.
- Reuse container processes through existing fixtures while isolating each scenario's mutable state.
- Use C# for test services and scripts. Do not introduce Python or SGP libraries.

## UI

- Follow [UI guidelines](doc/ui-guidelines.md). Verify changes in the actual Jellyfin frontend.
- Preserve native component upgrade conventions, full-width panels, shared input/button heights, accessible tooltips, and editable unsaved mappings.

## Delivery and safety

- Follow [release guidelines](doc/release-guidelines.md). Local installation, commits, pushes, tags, and deployment require the scope authorized in the current conversation.
- Never expose credentials, capability keys, or private paths in diagnostics or status payloads.
- Delete only owned generated files. Host database removal must never delete backing media.
- Treat unknown upstream identity, malformed responses, outages, and incomplete handoff as reasons to retain state.
- Do not describe an unverified change as installed, released, or proven in a real player.
