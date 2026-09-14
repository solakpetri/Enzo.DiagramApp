# Architecture

Enzo Diagrams follows Clean Architecture dependency direction so core DSL behavior remains independent from HTTP, CLI parsing, rendering libraries, temporary storage, Azure SDKs, and hosting concerns.

The refactor is complete in the current codebase. This document describes the implemented structure, not the original migration plan.

## Layers

| Layer | Project | Responsibility |
| --- | --- | --- |
| Domain | `src/Enzo.Diagrams.Domain` | Diagram models, lexer, parsers, validators, validation errors, and deterministic layout models/engines |
| Application | `src/Enzo.Diagrams.Application` | Use-case orchestration for validation/rendering, diagram complexity checks, renderer contract, and render-result storage contracts |
| Infrastructure | `src/Enzo.Diagrams.Infrastructure` | SVG renderers, PNG rasterization through Svg.Skia/SkiaSharp, local render-result storage, optional Azure Blob render-result storage |
| API | `src/Enzo.Diagrams.Api` | ASP.NET Core endpoints, request/response DTOs, API-key filter, ProblemDetails mapping, OpenAPI, configuration, limits, hosted-result endpoint, composition root |
| CLI | `src/Enzo.Diagrams.Cli` | `enzo-diagram` commands, argument parsing, console output, file input/output, exit codes, and composition root |

## Dependency Rule

Dependencies point inward. Domain does not reference any other Enzo project.

```text
src/Enzo.Diagrams.Domain
  -> no Enzo project references

src/Enzo.Diagrams.Application
  -> src/Enzo.Diagrams.Domain

src/Enzo.Diagrams.Infrastructure
  -> src/Enzo.Diagrams.Application
  -> src/Enzo.Diagrams.Domain

src/Enzo.Diagrams.Api
  -> src/Enzo.Diagrams.Application
  -> src/Enzo.Diagrams.Infrastructure

src/Enzo.Diagrams.Cli
  -> src/Enzo.Diagrams.Application
  -> src/Enzo.Diagrams.Infrastructure
```

Graphically:

```text
        API            CLI
         |              |
         v              v
       Infrastructure   |
         |              |
         v              v
       Application <----+
         |
         v
        Domain
```

API and CLI are composition roots. They may depend on Infrastructure to wire concrete implementations, but Domain and Application remain unaware of HTTP headers, ASP.NET Core request objects, CLI arguments, files, and Azure configuration.

## Parser Placement

The lexer and parsers live in Domain:

- `DiagramLexer`
- `DiagramParser`
- `FlowchartParser`
- `SequenceParser`
- `BpmnParser`

This is intentional because the parser defines the public Enzo DSL syntax and constructs core diagram models. Parser failures produce syntax diagnostics before validation rules are evaluated.

## Validator Placement

Validators live in Domain:

- `FlowchartValidator`
- `SequenceValidator`
- `BpmnValidator`

Current validation rules are diagram invariants: duplicate identifiers, unknown references, missing start/end elements, and cycle restrictions for flow/BPMN where applicable. Syntactically valid diagrams with semantic problems reach validation and return validation errors.

## Layout Placement

Layout engines live in Domain:

- `FlowchartLayoutEngine`
- `SequenceLayoutEngine`
- `BpmnLayoutEngine`

The current layout behavior is deterministic and renderer-independent. Layout models contain positions, bounds, and connection geometry but do not know SVG, PNG, SkiaSharp, HTTP, CLI, or storage.

## Renderer Boundary

Application defines `IDiagramRenderer` and `PngRenderOptions`. Infrastructure implements that boundary with:

- `InfrastructureDiagramRenderer`
- `DiagramSvgRenderer`
- `FlowchartSvgRenderer`
- `SequenceSvgRenderer`
- `BpmnSvgRenderer`
- `FlowchartPngRenderer`

SVG generation and PNG rasterization are infrastructure concerns because they produce output formats and PNG uses renderer-specific packages. User-controlled text is encoded by the SVG renderers before it enters markup.

## Application Use Cases

`DiagramService` is the main application service.

It supports:

- parse and validate source through Domain
- apply reusable diagram complexity limits
- render SVG through `IDiagramRenderer`
- render PNG from the generated SVG through `IDiagramRenderer`
- return application-level success/failure results without ASP.NET Core or CLI dependencies

Application also defines render-result storage contracts:

- `IRenderResultStore`
- `ILocalRenderResultReader`
- `StoredRenderResult`
- `StoredRenderResultContent`

These contracts support hosted PNG URL delivery without coupling Application to local files, Azure Blob Storage, or HTTP response handling.

## Composition Roots

The API composition root is `src/Enzo.Diagrams.Api/Program.cs`. It registers `DiagramService`, `InfrastructureDiagramRenderer`, local render-result storage, optional Azure Blob render-result storage, configuration validation, OpenAPI, ProblemDetails, and endpoint filters.

The CLI composition root is `CliComposition.CreateServices()` in `src/Enzo.Diagrams.Cli/Program.cs`. It registers `DiagramService` and `InfrastructureDiagramRenderer` for the `enzo-diagram` command.

## API And CLI Relationship

Both API and CLI delegate parsing, validation, and rendering to Application. They differ only in transport concerns:

- API maps JSON requests, `X-API-Key`, status codes, headers, ProblemDetails, raw file responses, and hosted URL responses.
- CLI maps command-line arguments, file paths, stdout/stderr messages, output files, and exit codes.

Application and Domain do not know about `X-API-Key`, HTTP content types, CLI options, stdout, stderr, or file-system output restrictions.

## Hosted Render Design

Hosted rendering remains supported for self-hosted API deployments.

`POST /v1/render` supports raw SVG/PNG delivery. When the request uses `format: "png"` and `delivery: "url"`, the API stores the generated PNG through `IRenderResultStore` and returns:

- `id`
- `format`
- `contentType`
- `url`
- `expiresAt`

The default local store writes temporary files and serves them through `GET /v1/render-results/{id}` using opaque 32-character lowercase hex identifiers. Expired local results are removed during store operations. Optional Azure Blob storage exists as an Infrastructure implementation, but the repository no longer automatically deploys to Azure and does not document an active production Azure endpoint.

## Where Does New Code Belong?

| Change | Layer |
| --- | --- |
| New diagram invariant | Domain |
| New DSL parsing behavior | Domain |
| New validation rule | Domain |
| New deterministic layout behavior | Domain |
| New use case or orchestration step | Application |
| New renderer implementation | Infrastructure |
| New rasterization or storage implementation | Infrastructure |
| New HTTP endpoint, request DTO, auth rule, status mapping | API |
| New CLI command, option, output formatting, file behavior | CLI |
| New benchmark scenario or metric | `benchmarks/` and benchmark tests |

## Non-Goals

The architecture intentionally avoids unnecessary framework or pattern weight. It does not introduce CQRS, MediatR, event buses, repositories for non-persistent models, microservices, or tactical DDD abstractions that do not solve a current problem.

Do not redesign the architecture merely because a different pattern could also work. Preserve the dependency direction and add tests when changing public DSL, rendering, API, CLI, or security behavior.
