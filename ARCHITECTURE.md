# Architecture

## Purpose

Enzo Diagrams follows Clean Architecture principles to keep core DSL behavior independent from transport, hosting, rendering libraries, storage, and command-line concerns. The structure makes parser, validation, layout, rendering orchestration, API endpoints, and CLI responsibilities easier to find and test without changing product behavior.

## Layers

| Layer | Responsibility |
| --- | --- |
| Domain | Core Enzo concepts, DSL parsing, validation rules, and deterministic layout models/engines |
| Application | Use cases and orchestration for validating and rendering diagrams; contracts for outward dependencies |
| Infrastructure | SVG/PNG renderer implementations, rasterization libraries, temporary render storage, Azure Blob storage integration, system/file implementations |
| API | HTTP endpoints, HTTP request/response DTOs, API-key authentication, ProblemDetails, OpenAPI, configuration, and composition root |
| CLI | Command-line argument parsing, console output, file input/output, exit codes, and composition root |

## Dependency Rule

Dependencies point inward:

```text
API -----------\
                v
CLI ------> Application ------> Domain
                ^
                |
         Infrastructure
```

Domain has no Enzo project dependencies and does not reference ASP.NET Core, Azure, CLI frameworks, rendering libraries, hosting, or dependency injection. Application depends on Domain and defines the renderer/storage contracts it needs. Infrastructure depends on Application and Domain to implement those contracts. API and CLI are outer composition roots that depend on Application and Infrastructure.

## Where Code Belongs

| Change | Layer |
| --- | --- |
| New DSL invariant | Domain |
| New parser or validator rule | Domain |
| New use case or orchestration step | Application |
| New renderer or rasterization implementation | Infrastructure |
| New temporary render storage implementation | Infrastructure |
| New API endpoint, HTTP DTO, auth rule, status mapping | API |
| New CLI command, option, output formatting, file behavior | CLI |

## Important Decisions

Parser lives in Domain because it defines the meaning of the Enzo DSL and constructs core diagram models.

Validator lives in Domain because the current rules are diagram invariants such as duplicate identifiers, missing participants, unsupported flows, and connection validity.

Layout lives in Domain because the current layout engines are deterministic diagram behavior and independent from SVG, PNG, SkiaSharp, HTTP, files, or hosting.

Renderer lives in Infrastructure because SVG generation and PNG rasterization are output implementations, and PNG depends on Svg.Skia/SkiaSharp and embedded font resources.

Hosted render storage contracts live in Application because hosted rendering is a use-case boundary. Local filesystem and Azure Blob storage implementations live in Infrastructure.

API-key authentication remains in API because `X-API-Key` is an HTTP concern. Domain and Application do not know the header name or authentication behavior.

CLI package identity remains `Enzo.Diagrams.Cli`, and the executable/tool name remains `enzo-diagram`.

## Non-Goals

This architecture does not attempt maximal abstraction, full DDD tactical patterns, CQRS, MediatR, command/query buses, event buses, repositories for non-persistent objects, or microservices. Pure deterministic code remains ordinary classes and functions unless an abstraction marks a real outward dependency.
