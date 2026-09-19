# Architecture Regression Report

## Scope

This branch verifies that the Clean Architecture refactor preserved observable behavior for the Enzo DSL, validation, rendering, API, CLI, hosted render support, benchmark infrastructure, and package metadata. It also rewrites public documentation to match the implemented architecture.

No product features were added. DSL behavior was not intentionally changed. Benchmark optimization and live OpenAI benchmarking were not resumed.

## Architecture Tested

| Layer | Status | Notes |
| --- | --- | --- |
| Domain | Pass | Parser, validator, layout, and documentation fixtures covered. |
| Application | Pass | New `DiagramService` tests cover validate/render orchestration without ASP.NET or CLI. |
| Infrastructure | Pass | SVG, PNG, escaping, rasterization, and local hosted storage covered by existing tests. |
| API | Pass | HTTP contracts, authentication, limits, hosted render, OpenAPI, and error safety covered. |
| CLI | Pass | Validate/render commands, SVG/PNG output, invalid input, path restrictions, and package metadata covered. |
| Benchmarks | Pass | Offline benchmark scenario loading, metrics, LLM-result calculations, and semantic reevaluation tests pass. |

## Coverage Summary

| Area | Status | Notes |
| --- | --- | --- |
| Parser | Pass | Flow, sequence, BPMN, syntax failures, documentation examples, and representative sequence fixture. |
| Validator | Pass | Duplicate identifiers, unknown references, missing start/end rules, cycles where supported, deterministic diagnostics. |
| Layout | Pass | Flow, sequence, and BPMN layout invariants and deterministic positioning tests. |
| SVG | Pass | Render succeeds for all diagram kinds; expected labels/shapes present; user text escaping tested. |
| PNG | Pass | Representative SVG rasterization, valid PNG signature, rendered text, and size-limit failure tests. |
| API | Pass | `/v1/validate`, `/v1/render`, raw SVG/PNG, hosted PNG URLs, auth, limits, OpenAPI, and safe errors. |
| CLI | Pass | Public `validate` and `render` behavior, SVG/PNG output, invalid input, and CLI/Application SVG parity. |
| Architecture | Pass | Project references and Domain/Application package dependency boundaries tested. |
| Benchmarks | Pass | Compiles and tests offline only; no live OpenAI calls made. |
| NuGet/tool | Pass | `dotnet pack src\Enzo.Diagrams.Cli\Enzo.Diagrams.Cli.csproj -c Release` succeeds; `PackageId` remains `Enzo.Diagrams.Cli`. |
| Docker | Gap | Docker CLI exists, but Docker Desktop Linux engine was not running, so `docker build` could not connect to the daemon. |

## Parser Coverage

Regression tests cover:

- `flow` minimal and realistic diagrams.
- `sequence` actors, participants, request/response messages, technical labels, punctuation/numeric labels, and long checkout-style interactions.
- `bpmn` start, task, gateway, end, labeled branches, and unsupported declarations.
- Parser/validator boundary where malformed syntax fails parsing and syntactically valid undefined references reach validation.
- Documentation assets under `docs/assets/*.enzo`.

## Validation Coverage

Regression tests cover:

- duplicate flow node identifiers
- duplicate sequence participant identifiers
- duplicate BPMN element identifiers
- unknown flow edge sources/targets
- unknown sequence message sources/targets
- unknown BPMN flow sources/targets
- missing flow start/end nodes
- missing BPMN start/end events
- flow and BPMN cycle detection
- useful deterministic diagnostic kinds and message fragments

## Rendering Coverage

SVG rendering is covered for flow, sequence, and BPMN. Tests assert generated SVG structure, diagram labels, cross-platform font family, sequence response styling, BPMN shapes, and XML escaping of user-controlled labels/messages.

PNG rendering is covered with valid PNG signatures, representative flow/sequence/BPMN diagrams, rendered text pixel differences, invalid SVG failure, and configured dimension/pixel limits.

## API Coverage

API tests use ASP.NET Core `WebApplicationFactory` and exercise the real HTTP layer. Covered behavior includes:

- `POST /v1/validate` valid source, invalid DSL, syntax errors, missing/malformed JSON behavior, and limits.
- `POST /v1/render` SVG, PNG, unsupported format, missing format, raw delivery, URL delivery, and unsupported delivery combinations.
- `X-API-Key` valid, missing, empty, invalid, and duplicate-key behavior.
- hosted PNG artifact creation, retrieval, content type, no-store cache behavior, opaque ID shape, storage failures, and production-style generic errors.
- OpenAPI contract shape and API-key security scheme.

## CLI Coverage

CLI tests cover:

- `enzo-diagram validate <file>` success and failure.
- `enzo-diagram render <file>` default SVG output.
- `--format svg|png`.
- `--output <file>` within the source directory.
- invalid DSL and missing/invalid output behavior.
- cancellation handling.
- CLI/Application SVG parity for the same source.
- package metadata for `Enzo.Diagrams.Cli` and `enzo-diagram`.

The current CLI does not expose separate `--help` or `--version` options; incomplete or invalid arguments print usage and return a non-zero exit code.

## Security Regression Coverage

Security-sensitive tests cover:

- SVG escaping for `<script>`, `<foreignObject>`, event-style attributes, `javascript:`, quotes, ampersands, and closing text fragments.
- rendered-image security headers.
- API key failure responses that do not echo configured or provided keys.
- hosted storage failures that do not expose secrets.
- production unexpected errors that return generic ProblemDetails.
- request body/source/diagram complexity limits and PNG rendering limits.

## Architecture-Boundary Coverage

Architecture tests verify:

- Domain has no Enzo project references.
- Application references only Domain.
- Infrastructure references Application and Domain.
- API and CLI reference Application and Infrastructure.
- Domain has no package references.
- Application remains free from ASP.NET Core, System.CommandLine, Azure, SkiaSharp, and Svg.Skia packages.
- API and CLI composition roots can resolve `DiagramService` and renderer/storage dependencies.

## Build And Package Verification

Commands run successfully:

```powershell
dotnet restore Enzo.Diagrams.sln
dotnet build Enzo.Diagrams.sln --no-restore
dotnet test Enzo.Diagrams.sln --no-build
dotnet pack src\Enzo.Diagrams.Cli\Enzo.Diagrams.Cli.csproj -c Release
```

Results:

- Build: pass, 0 warnings, 0 errors.
- Tests: pass, 202 tests.
- Pack: pass, package `Enzo.Diagrams.Cli.0.1.0.nupkg` created. NuGet emitted the existing warning that the package has no readme.

Docker command attempted:

```powershell
docker build -f src\Enzo.Diagrams.Api\Dockerfile -t enzo-diagrams-api:architecture-regression .
```

Result: not verified because Docker could not connect to `dockerDesktopLinuxEngine`; the daemon was not running in this environment. The Dockerfile was corrected to copy the refactored Domain/Application/Infrastructure projects instead of removed Core/Language/Rendering projects.

## Documentation Rewritten

Updated documentation:

- `README.md`
- `ARCHITECTURE.md`
- `docs/CLEAN_ARCHITECTURE_REFACTOR.md`
- `docs/api-authentication.md`
- `SECURITY.md`
- `CONTRIBUTING.md`
- `PUBLIC_RELEASE_CHECKLIST.md`
- `benchmarks/README.md`

Documentation now reflects the implemented architecture, current API/CLI behavior, self-hosting status, stopped Azure hosting, retired MCP/Apps SDK/frontend features, benchmark limitations, and repository licensing status.

## Known Gaps

- Actual NuGet publication was not performed.
- External Azure resources and GitHub repository settings were not verified.
- Docker image build could not be completed because the Docker daemon was unavailable.
- Live OpenAI/GPT benchmark execution was intentionally not performed.
- All possible visual renderer regressions are not exhaustively snapshot-tested.
- Repository licensing is now stated in the root MIT license file.

## Final Result

The Clean Architecture refactor is regression-verified at the repository level. Supported DSL kinds parse and validate, SVG/PNG rendering works, API and CLI public behavior is covered, security hardening remains covered, architecture dependency direction is tested, benchmarks compile and test offline, and public documentation now matches the implemented codebase.
