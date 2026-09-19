# Contributing

## Build And Test

Use the normal .NET workflow from the repository root:

```powershell
dotnet restore
dotnet build
dotnet test
```

The full test suite must run without Azure, OpenAI, production API keys, or other live external services.

Pack the CLI locally when changing tool packaging or project metadata:

```powershell
dotnet pack src/Enzo.Diagrams.Cli\Enzo.Diagrams.Cli.csproj -c Release
```

If Docker support is affected and Docker is available, build the API image locally:

```powershell
docker build -f src/Enzo.Diagrams.Api/Dockerfile -t enzo-diagrams-api:local .
```

## Architecture Rules

Respect the current Clean Architecture dependency direction:

```text
API / CLI -> Infrastructure -> Application -> Domain
API / CLI -> Application -> Domain
```

- Domain must not reference Application, Infrastructure, API, CLI, ASP.NET Core, Azure, System.CommandLine, or renderer-specific packages.
- Application must not reference Infrastructure, API, CLI, ASP.NET Core request objects, CLI parsing, Azure, or renderer-specific packages.
- Infrastructure implements Application contracts for rendering and storage.
- API and CLI are composition roots and transport boundaries.

See [ARCHITECTURE.md](ARCHITECTURE.md) for where new code belongs.

## DSL Changes

Do not change Enzo DSL behavior casually. The public DSL includes parser behavior, validator behavior, layout behavior, rendering behavior, API contracts, CLI behavior, benchmark semantics, and NuGet CLI package behavior.

DSL changes require:

- parser regression tests
- validator tests for valid and invalid cases
- rendering tests when output can change
- API/CLI tests when public behavior changes
- documentation updates for examples and syntax
- benchmark fixture review when benchmark scenarios are affected

Preserve the supported diagram kinds: `flow`, `sequence`, and the lightweight BPMN-inspired `bpmn` subset.

## Testing Expectations

Prefer the smallest test level that proves the behavior:

- Domain tests for parser, validator, layout, identifiers, references, cycles, and diagram invariants.
- Application tests for parse/validate/render use cases and reusable limits.
- Infrastructure tests for SVG, PNG, escaping, rasterization, and storage implementations.
- API tests for HTTP routing, request/response contracts, authentication, limits, content types, and ProblemDetails.
- CLI tests for commands, options, output files, stdout/stderr, and exit codes.
- Benchmark tests for offline scenario loading, metrics, semantic calculations, and report parsing.

Avoid excessive mocking of deterministic Enzo components. Do not test private methods just because they exist.

## Rendering And Security

User-controlled text flows from DSL source into SVG and PNG output. Changes in parsing, layout, SVG generation, PNG rasterization, API responses, or hosted render storage must consider:

- SVG/XML injection
- `javascript:` and event-handler injection
- request-size and diagram-complexity abuse
- PNG image-size abuse
- error responses exposing paths, stack traces, configuration values, API keys, or connection strings
- hosted render URL predictability and expiration

Add regression tests for security-sensitive rendering behavior, especially SVG escaping and oversized input rejection.

## Benchmarks

Benchmark reports under `benchmarks/results/` are historical evidence. Do not delete failed or unfavorable results merely because they show regressions.

Do not make live LLM/OpenAI calls in ordinary build/test validation. Benchmark regression work should compile the infrastructure and test offline scenario/metric behavior only unless a branch is explicitly about live benchmarking.

## Branches And Commits

Use focused branches and conventional commit messages such as `fix:`, `test:`, `docs:`, and `chore:`. Keep unrelated cleanup separate from behavior changes.

Do not mix architecture redesign, feature work, benchmark optimization, and documentation cleanup in one change unless explicitly requested.
