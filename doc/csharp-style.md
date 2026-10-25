# C# readability and layout

Use the repository editorconfig and its 180-character line limit. Keep related work together and separate distinct phases with a blank line.

## Blocks and calls

- Always use braces for control-flow bodies. Opening and closing braces occupy separate lines, including short or empty blocks and initializers.
- Keep short calls inline when readable. When chopping, put each argument on its own line and the closing parenthesis on its own line.
- Name every supplied argument in a chopped call with more than one argument. Omit the name for a single argument unless it selects a later optional parameter or is required for binding.
- Name intermediate requests, calculations, and results instead of nesting operations inside another call.
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
