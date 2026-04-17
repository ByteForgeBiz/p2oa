# AGENTS.md

This file provides guidance to Codex when working with code in this repository.

## Project Overview

**Postman OpenAPI Converter** (`p2oa`) is a .NET 10 console application for bidirectional conversion between:

- Postman collection JSON
- OpenAPI YAML
- Postman's Git-compatible YAML directory structure

The repository includes the CLI app, an xUnit test project, sample fixtures under `Examples/`, and Windows packaging assets for portable and installer releases.

## Tech Stack

- **Language:** C# with nullable reference types and implicit usings enabled
- **Framework:** .NET 10 (`net10.0`)
- **CLI parsing:** `System.CommandLine` 2.0.3
- **OpenAPI model + serialization:** `Microsoft.OpenApi` 3.4.0
- **YAML serialization:** `YamlDotNet` 15.1.2
- **Single-file dependency embedding:** `Costura.Fody` 6.0.0
- **Tests:** xUnit 2.9.3, FluentAssertions 8.8.0, coverlet.collector 6.0.4

## Common Commands

```bash
# Build the main project
dotnet build PostmanOpenAPIConverter/PostmanOpenAPIConverter.csproj

# Build the whole solution
dotnet build PostmanOpenAPIConverter.slnx

# Run all tests
dotnet test PostmanOpenAPIConverter.slnx

# Run a single test by name
dotnet test --filter "FullyQualifiedName~PostmanToOpenApiConverterTests"

# Convert Postman JSON to OpenAPI YAML (stdout)
dotnet run --project PostmanOpenAPIConverter -- to-openapi -i collection.json

# Convert Postman JSON to OpenAPI YAML file
dotnet run --project PostmanOpenAPIConverter -- to-openapi -i collection.json -o openapi.yaml

# Target a specific OpenAPI version
dotnet run --project PostmanOpenAPIConverter -- to-openapi -i collection.json -o openapi.yaml -v 3.0

# Convert Postman JSON to Postman Git-compatible YAML tree
dotnet run --project PostmanOpenAPIConverter -- to-postman-git -i collection.json -o out-dir

# Convert Postman Git-compatible YAML tree back to Postman JSON
dotnet run --project PostmanOpenAPIConverter -- from-postman-git -i out-dir -o collection.json
```

Useful note:

- The CLI prints a banner to stderr by default; use `--no-banner`, `--quiet`, or `-q` to suppress it in scripts.

## Project Structure

```text
PostmanToOpenAPI/
  PostmanOpenAPIConverter.slnx
  AGENTS.md
  README.md
  Examples/
    Postman/
    OpenAPI/
    GIT/
  PostmanOpenAPIConverter/
    Program.cs
    PostmanOpenAPIConverter.csproj
    Converters/
      PostmanToOpenApiConverter.cs
      PostmanToGitYamlConverter.cs
      PostmanGitYamlToJsonConverter.cs
    Models/
      PostmanCollection.cs
      GitYamlModels.cs
  PostmanOpenAPIConverter.Tests/
    PostmanOpenAPIConverter.Tests.csproj
    PostmanToOpenApiConverterTests.cs
    PostmanToGitYamlConverterTests.cs
    PostmanGitYamlToJsonConverterTests.cs
    TestData/
  installer/
    build-installer.bat
    p2oa.nsi
  .github/
    workflows/
```

Ignore generated output in `bin/` and `obj/` unless the task is specifically about build artifacts.

## Architecture

### CLI entry point (`PostmanOpenAPIConverter/Program.cs`)

The application exposes three subcommands:

- `to-openapi`: Postman JSON -> OpenAPI YAML
- `to-postman-git`: Postman JSON -> Postman Git-compatible YAML tree
- `from-postman-git`: Postman Git-compatible YAML tree -> Postman JSON

Important CLI behavior:

- `to-openapi` defaults to OpenAPI `3.1`
- Supported version strings are `3.1`, `3.0`, `3.2`, and `2.0`
- `from-postman-git` accepts either the repo root or a single collection directory
- All command handlers catch exceptions, print a single error message to stderr, and exit with code `1`

### Postman models (`PostmanOpenAPIConverter/Models/PostmanCollection.cs`)

These models deserialize Postman v2.0 and v2.1 collections and smooth over format differences with custom JSON converters.

Notable quirks handled here:

- Descriptions may be plain strings or structured objects
- URLs may be strings or structured objects
- Path segments may be strings or `{ type, value }` objects
- Optional request/auth/body/script sections are modeled loosely enough to round-trip common Postman exports

### Git YAML models (`PostmanOpenAPIConverter/Models/GitYamlModels.cs`)

These classes define the YAML shape used for Postman's Git-compatible directory layout, including:

- collection/folder metadata in `.resources/definition.yaml`
- request files as `*.request.yaml`
- workspace metadata in `.postman/resources.yaml`
- globals in `postman/globals/workspace.globals.yaml`

### Postman -> OpenAPI (`PostmanOpenAPIConverter/Converters/PostmanToOpenApiConverter.cs`)

Pipeline:

1. Deserialize Postman JSON
2. Flatten nested items
3. Build an `OpenApiDocument`
4. Serialize YAML for the requested OpenAPI version

Key decisions:

- Folder names become OpenAPI tags
- `:param` and `{{param}}` path segments normalize to `{param}`
- Raw URLs are used as a fallback when structured URL parts are missing
- Host variables such as `{{baseUrl}}` normalize into OpenAPI server variables
- Content type is inferred from headers, body options, and body content

### Postman -> Git YAML (`PostmanOpenAPIConverter/Converters/PostmanToGitYamlConverter.cs`)

This converter writes the directory tree expected by Postman's Git mode.

Key decisions:

- Creates `.postman/resources.yaml` and `postman/globals/workspace.globals.yaml`
- Writes collection/folder metadata to `.resources/definition.yaml`
- Writes requests as `{Name}.request.yaml`
- Preserves ordering using numeric `order` values
- Carries across variables, auth, scripts, headers, query params, path variables, and supported body shapes

### Git YAML -> Postman (`PostmanOpenAPIConverter/Converters/PostmanGitYamlToJsonConverter.cs`)

This converter reconstructs a Postman collection from a Git YAML tree.

Key decisions:

- Accepts either a collection directory or repo root as input
- Auto-detects a single collection; requires `--collection` when multiple collections exist
- Rebuilds folder/request ordering from `order`
- Rehydrates request metadata, variables, auth, scripts, and body content into Postman v2.1 JSON

## Testing Guidance

Tests live in `PostmanOpenAPIConverter.Tests/` and are split by converter direction:

- `PostmanToOpenApiConverterTests.cs`
- `PostmanToGitYamlConverterTests.cs`
- `PostmanGitYamlToJsonConverterTests.cs`

When changing conversion logic:

- add or update targeted tests in the matching test file
- prefer fixture-driven tests using `PostmanOpenAPIConverter.Tests/TestData/`
- verify both happy paths and edge cases around folders, variables, auth, and body serialization

## Packaging And Release Notes

- The executable assembly name is `p2oa`
- Version metadata is generated in the project file using the current date
- `installer/` contains NSIS packaging assets
- `.github/workflows/` contains release automation, including Windows packaging/publishing workflows

If you touch packaging or release logic, inspect both the project file and the workflow/installer files together before editing.

## Working Conventions For Agents

- Prefer `rg` / `rg --files` for searching
- Read the existing converter/tests before changing conversion rules; subtle round-trip behavior matters here
- Keep changes compatible with the current CLI surface unless the task explicitly asks for a breaking change
- If you change serialization shape, update tests and relevant examples or documentation in the same pass
- Avoid editing generated files under `bin/` or `obj/`
