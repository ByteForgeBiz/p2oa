---
applyTo: "**/*.cs"
---

# Code Review Guidelines — p2oa (PostmanOpenAPIConverter)

C# 13 / .NET 10 console application. Converts Postman collections (v2.0 and v2.1) to OpenAPI YAML and to Postman's Git-compatible YAML format.

---

## Nullable Reference Types

- All reference types must be correctly annotated (`?` where null is expected).
- Do not suppress nullable warnings with `!` unless the null-impossibility is obvious and a comment explains why.
- Prefer pattern matching (`is not null`, `is { }`) over null checks with `!=`.

## Naming and Style

- Types and public methods: PascalCase.
- Local variables and parameters: camelCase.
- Private fields: camelCase (no underscore prefix).
- Use `var` when the type is evident from the right-hand side; use explicit types otherwise.
- Prefer `is` pattern matching over explicit casts (`(Type)x`).
- Use `switch` expressions over `switch` statements where the result is a value.

## Error Handling

- Throw `InvalidOperationException` (not `Exception`) for logic failures with a descriptive message.
- Wrap third-party parsing calls (JSON, YAML) in try/catch; re-throw as `InvalidOperationException` with context.
- Do not swallow exceptions silently. Log or rethrow.
- Validate method parameters only at system boundaries (CLI input, file reads). Trust internal models.

## LINQ and Collections

- Prefer LINQ for data transformations; avoid multi-step loops that could be a single expression.
- Do not call `.ToList()` or `.ToArray()` unless the result is iterated more than once or passed to an API that requires it.
- Use `[.. expr]` (collection expressions, C# 12+) over `new List<T>(expr)` for concise initialization.

## String Handling

- Always pass `StringComparison.OrdinalIgnoreCase` for case-insensitive comparisons.
- Use `string.Equals(a, b, StringComparison)` or `.Equals(StringComparison)`, not `ToLower()`/`ToUpper()` comparisons.

## Async

- Methods that perform I/O must be `async` and use `await`; do not use `.Result` or `.Wait()`.
- Pass `CancellationToken` through async call chains where applicable.

## Resources and Disposal

- Dispose `Stream`, `TextWriter`, `HttpClient`, and similar types with `using` declarations.
- Prefer `await using` for async disposable types.

## Security

- Do not construct file paths by concatenating user input directly; use `Path.Combine`.
- Do not execute shell commands with user-supplied strings.
- Do not log or expose raw exception messages to end users; surface a sanitized message instead.

## XML Documentation

- All types and members must have `<summary>` XML documentation, regardless of their visibility.
- `<param>`, `<returns>`, and `<exception>` tags are required on public methods when applicable.

## Patterns Specific to This Codebase

- Custom `JsonConverter<T>` subclasses must handle all `JsonTokenType` cases and call `reader.Skip()` in the default branch.
- MSBuild `.targets` files: use `BeforeTargets="GenerateAssemblyInfo"` (not `PrepareForBuild`) for SDK-style projects.
- NSIS scripts: version defines must use `!ifndef` guards so they can be overridden from the command line.
- Avoid `static` mutable state in converter classes; prefer `static readonly` for shared serializer options.
