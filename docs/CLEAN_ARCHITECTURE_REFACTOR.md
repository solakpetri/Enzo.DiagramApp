# Clean Architecture Refactor

This document records the completed refactor from the earlier project layout to the current Clean Architecture layout. For ongoing architecture guidance, see [ARCHITECTURE.md](../ARCHITECTURE.md).

## Previous Structure

The solution previously used:

- `Enzo.Diagrams.Language` for DSL models, parsing, validation, and layout.
- `Enzo.Diagrams.Rendering` for SVG and PNG output.
- `Enzo.Diagrams.Api` for endpoints plus hosted render storage concerns.
- `Enzo.Diagrams.Cli` for command-line orchestration.
- an empty `Enzo.Diagrams.Core` project.

API and CLI directly parsed and rendered diagrams, so use-case orchestration was duplicated at the outer layers. Hosted render storage also lived at the API boundary.

## Current Structure

```text
src/
  Enzo.Diagrams.Domain/
  Enzo.Diagrams.Application/
  Enzo.Diagrams.Infrastructure/
  Enzo.Diagrams.Api/
  Enzo.Diagrams.Cli/
```

The old Core project was removed. Language responsibilities moved to Domain. Rendering responsibilities moved to Infrastructure. Application was introduced as the shared use-case layer used by both API and CLI.

## Components Moved

| Component | Current Location |
| --- | --- |
| Diagram models, lexer, parsers, syntax errors | `Enzo.Diagrams.Domain` |
| Flowchart, sequence, and BPMN validators | `Enzo.Diagrams.Domain` |
| Flowchart, sequence, and BPMN layout engines | `Enzo.Diagrams.Domain` |
| Validation/render orchestration | `Enzo.Diagrams.Application.DiagramService` |
| Renderer and render-result storage contracts | `Enzo.Diagrams.Application` |
| SVG and PNG renderer implementations | `Enzo.Diagrams.Infrastructure` |
| Local and optional Azure render-result storage | `Enzo.Diagrams.Infrastructure` |
| HTTP request contracts, authentication, limits, hosted URL mapping | `Enzo.Diagrams.Api` |
| CLI command parsing, file behavior, stdout/stderr, exit codes | `Enzo.Diagrams.Cli` |

## Dependency Direction

Implemented project references are:

```text
Domain -> no Enzo project dependencies
Application -> Domain
Infrastructure -> Application, Domain
API -> Application, Infrastructure
CLI -> Application, Infrastructure
```

Lightweight architecture tests enforce this direction and verify that Domain/Application do not acquire outer framework packages such as ASP.NET Core, Azure, System.CommandLine, SkiaSharp, or Svg.Skia.

## Behavior Preserved

The refactor was intended to preserve:

- DSL syntax
- parser behavior
- validator behavior
- layout behavior
- SVG rendering behavior
- PNG rendering behavior
- API request and response contracts
- API-key authentication through `X-API-Key`
- CLI commands, options, output behavior, and exit codes
- hosted render behavior for self-hosted deployments
- benchmark scenario and metric semantics
- NuGet CLI package identity `Enzo.Diagrams.Cli`

The architecture regression branch adds characterization tests and documentation updates for these areas.

## Current Compromises

- Parser, validation, and layout remain in Domain because they define deterministic Enzo DSL behavior.
- Rendering is in Infrastructure because SVG/PNG output and PNG rasterization are implementation concerns.
- Application owns render-result storage contracts so hosted rendering can be orchestrated without coupling use cases to local files or Azure SDKs.
- API request body/source limits remain at the HTTP boundary, while parsed diagram complexity limits are reusable through Application.
- API and CLI are separate composition roots instead of sharing transport-specific code.

## Azure Status

Enzo Diagrams was previously deployed and tested successfully on Azure Container Apps. The hosted environment was later stopped, and the repository no longer automatically deploys to Azure.

Azure Blob storage remains an optional Infrastructure implementation for self-hosted hosted-render deployments. It is not an active repository deployment target.
