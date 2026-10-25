# C# readability and layout

Use the repository editorconfig and its 180-character line limit. Keep related work together and separate distinct phases with a blank line.

## Blocks and calls

- Always use braces for control-flow bodies. Nonempty blocks and initializers use separate opening and closing lines. Auto-properties and empty braces may stay compact.
- Prefer one line for short, simple calls such as Version.TryParse and Path.Combine; omit redundant names. Chop only for length, many arguments, complexity, or useful semantic grouping.
- When chopping, put each argument on its own line and the closing parenthesis on its own line.
- Name every supplied argument in a chopped call with more than one argument. Omit the name for a single argument unless it selects a later optional parameter or is required for binding.
- Split deeply nested operations into named intermediates. Inline a small request record in its Mediator call when that is shorter and clearer; avoid locals that merely repeat a type name.
- Declare locals near their first use. Use descriptive names; avoid compressed names that make the reader infer their role.

## Methods and results

- Prefer short methods that perform one understandable phase. Use guard returns to keep the main path shallow.
- Extract nested work into a named method with a useful result. Do not introduce a general framework or a wrapper that only obscures a call.
- Arrange helpers in depth-first call order where it makes a workflow easier to follow. Keep related entry points together when that reads better.
- Return semantic records or readonly record structs when several values form one result. Public methods must not return tuples.
- Keep requests and per-operation state data-only. Compose services through DI, and let the Logic root own application registrations.

## Comments and verification

Explain non-obvious ownership, lifetime, timing, failure retention, or algorithms. Document public contracts; do not narrate obvious statements.

Formatting must preserve protocol payloads, diagnostics, cancellation, disposal, and validation order. Review the diff and run the relevant checks after structural edits.
