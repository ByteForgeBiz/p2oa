---
applyTo: "**/*.nsi"
---

# NSIS Script Guidelines — p2oa (PostmanOpenAPIConverter)

## Patterns Specific to This Codebase

- Version defines must use `!ifndef` guards so they can be overridden from the command line.
