---
applyTo: "**/*.targets"
---

# MSBuild Guidelines — p2oa (PostmanOpenAPIConverter)

## Patterns Specific to This Codebase

- Use `BeforeTargets="GenerateAssemblyInfo"` (not `PrepareForBuild`) for targets that must run before assembly info is generated in SDK-style projects.
