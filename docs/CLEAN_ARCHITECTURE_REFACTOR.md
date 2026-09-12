# Clean Architecture Refactor

## Previous Structure

The solution previously used `Enzo.Diagrams.Language` for DSL models, parser, validators, and layout; `Enzo.Diagrams.Rendering` for SVG/PNG output; `Enzo.Diagrams.Api` for endpoints plus hosted storage; `Enzo.Diagrams.Cli` for command-line orchestration; and an empty `Enzo.Diagrams.Core` project.

API and CLI both directly parsed and rendered diagrams, so use-case orchestration was duplicated at the outer layers. Hosted render storage lived in the API project and brought Azure-specific implementation concerns into the HTTP layer.

## New Structure

```text
src/
  Enzo.Diagrams.Domain/
  Enzo.Diagrams.Application/
  Enzo.Diagrams.Infrastructure/
  Enzo.Diagrams.Api/
  Enzo.Diagrams.Cli/
```

The old empty Core project was removed. Language became Domain. Rendering became Infrastructure. Application was introduced as the shared use-case layer.

## Major Components Moved

| Component | New Location |
| --- | --- |
| Diagram models, parser, lexer, syntax errors | `Enzo.Diagrams.Domain` |
| Flowchart, sequence, and BPMN validators | `Enzo.Diagrams.Domain` |
| Flowchart, sequence, and BPMN layout engines | `Enzo.Diagrams.Domain` |
| Validation/render orchestration | `Enzo.Diagrams.Application.DiagramService` |
| Renderer contracts and render result storage contracts | `Enzo.Diagrams.Application` |
| SVG and PNG renderer implementations | `Enzo.Diagrams.Infrastructure` |
| Local and Azure hosted render storage | `Enzo.Diagrams.Infrastructure` |

## Dependency Direction

Project references now follow the intended direction:

```text
Domain -> no Enzo project dependencies
Application -> Domain
Infrastructure -> Application, Domain
API -> Application, Infrastructure
CLI -> Application, Infrastructure
```

Benchmarks continue to compile against the refactored projects without changing scenarios, prompts, metrics, semantic scoring, or historical results.

## Architectural Decisions

Parser remains inward in Domain because it defines the Enzo DSL semantics.

Validator remains inward in Domain because the current checks are core diagram invariants.

Layout remains inward in Domain because it is deterministic and renderer-independent.

Rendering moved outward to Infrastructure because SVG/PNG output is an implementation concern and PNG uses external rasterization libraries.

Hosted render storage now uses Application contracts with Infrastructure implementations. API still controls HTTP response mapping, URL delivery validation, and hosted-result endpoint behavior.

## Compromises

Public renderer static classes remain available in Infrastructure to keep tests and benchmark callers simple during this structural migration.

API request body/source limits remain in the API layer because they are HTTP boundary concerns. Diagram element/connection complexity is enforced through Application orchestration so API and future callers can reuse the same rule.

No architecture-testing package was introduced. Lightweight tests inspect project references and composition resolution.

## Behavior Intentionally Preserved

DSL syntax, parser behavior, validator behavior, layout behavior, SVG rendering, PNG rendering, API routes/contracts, API-key behavior, CLI commands/options/output, hosted-render behavior, benchmark semantics, benchmark scenarios, and CLI NuGet package identity were intentionally preserved.

## Areas Deliberately Not Changed

No new diagram features were added. Benchmark optimization was not continued. Azure-specific storage was not expanded. API versioning, CLI UX, documentation rewrite, and comprehensive characterization coverage are left for the follow-up branch.

## Tests Run

```text
dotnet restore Enzo.Diagrams.sln
dotnet build Enzo.Diagrams.sln --no-restore
dotnet test Enzo.Diagrams.sln --no-build
```

## Follow-Up Recommendations

The next planned branch is:

```text
test/architecture-regression
```

That branch will comprehensively regression-test behavior, add missing characterization tests, test API contracts, test CLI behavior, test parser/validator/render output, test security-sensitive behavior, and update or rewrite documentation to reflect the final architecture.
