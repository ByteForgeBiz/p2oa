# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

**PostmanOpenAPIConverter** is a .NET 10 console application for bidirectional conversion between Postman collection files and OpenAPI specifications. The primary use case is Postman's "GIT compatible" mode, which stores collections as OpenAPI YAML.

## Tech Stack

- **Language:** C# with nullable reference types and implicit usings enabled
- **Framework:** .NET 10 (`net10.0`)
- **Key packages:**
  - `Microsoft.OpenApi` 1.6.22 — OpenAPI object model + YAML/JSON serialization (supports OpenAPI 2.0 and 3.0; 3.1 requires upgrading to Microsoft.OpenApi 2.x)
  - `System.CommandLine` 2.0.0-beta4 — CLI parsing
  - `System.Text.Json` (SDK-included) — Postman JSON deserialization

## Common Commands

```bash
# Build
dotnet build PostmanOpenAPIConverter/PostmanOpenAPIConverter.csproj

# Convert a Postman collection to OpenAPI YAML (stdout)
dotnet run --project PostmanOpenAPIConverter -- to-openapi --input collection.json

# Convert and write to file, targeting OpenAPI 3.0 (default)
dotnet run --project PostmanOpenAPIConverter -- to-openapi -i collection.json -o openapi.yaml

# Target a different OpenAPI version (3.1 needs Microsoft.OpenApi 2.x upgrade)
dotnet run --project PostmanOpenAPIConverter -- to-openapi -i collection.json --openapi-version 2.0

# Run tests (once a test project is added)
dotnet test

# Run a single test
dotnet test --filter "FullyQualifiedName~TestMethodName"
```

## Project Structure

```
PostmanToOpenAPI/                      # repo root (folder not yet renamed)
  PostmanOpenAPIConverter.slnx
  PostmanOpenAPIConverter/
    Program.cs                         # CLI entry point (System.CommandLine)
    Models/
      PostmanCollection.cs             # Postman v2.0/v2.1 deserialization models
    Converters/
      PostmanToOpenApiConverter.cs     # Postman → OpenAPI conversion logic
```

## Architecture

### Postman models (`Models/PostmanCollection.cs`)

Handles quirks of both v2.0 and v2.1 collections via custom `JsonConverter`s:
- `DescriptionConverter` — description is a plain string in v2.0, or `{"content":"...","type":"text/markdown"}` in v2.1
- `PostmanUrlConverter` — URL is a plain string in v2.0, or a structured object in v2.1; also handles path segments that are objects `{type, value}` rather than strings

### Converter (`Converters/PostmanToOpenApiConverter.cs`)

Pipeline: deserialize JSON → `BuildDocument()` → serialize YAML

Key decisions:
- `FlattenItems()` traverses the item tree recursively; folder names become OpenAPI tags
- Path variables: prefers the explicit `url.variable` array (v2.1); falls back to scanning for `{param}` tokens extracted from `:param` / `{{param}}` segments (v2.0)
- `{{baseUrl}}` host → OpenAPI server variable `{baseUrl}` with an empty default
- Content-type resolution order: explicit `Content-Type` header → body language option → raw body sniffing → `application/json`
- OpenAPI 3.1 output is wired up in the enum/CLI but requires upgrading to `Microsoft.OpenApi` 2.x to serialize

### CLI (`Program.cs`)

Single root command with subcommands — `to-openapi` is implemented; `to-postman` (reverse direction) is the next planned subcommand.
