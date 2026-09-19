# Enzo Diagrams

Enzo Diagrams is an AI-first diagram-as-code engine built in .NET. It uses a compact DSL to parse, validate, lay out, and render sequence diagrams, flow diagrams, and lightweight BPMN as SVG or PNG.

The engine is deterministic and AI-provider-independent. AI agents can generate Enzo DSL and call the API or CLI, but this repository does not call OpenAI or any other LLM provider during normal operation.

## Why Enzo

- Compact DSL for software-oriented diagrams.
- Parser, validator, layout, SVG rendering, and PNG rendering owned by the repository.
- Headless CLI and HTTP API for automation.
- Strongest benchmarked use case is sequence-diagram generation.
- Normal build and tests do not require Azure, OpenAI, production API keys, or private infrastructure.

Enzo is not positioned as a full Mermaid replacement. Mermaid remains broader and more familiar to models; Enzo focuses on a smaller AI-oriented workflow.

## Features

- Diagram kinds: `sequence`, `flow`, and a lightweight `bpmn` subset.
- Output formats: SVG and PNG.
- Public interfaces: .NET global tool and ASP.NET Core HTTP API.
- API-key protected validation and rendering endpoints.
- Optional self-hosted temporary PNG URL delivery.
- Offline benchmark and regression test infrastructure.

## Quick Start

Install the CLI:

```powershell
dotnet tool install --global Enzo.Diagrams.Cli
```

Create `checkout.enzo`:

```text
sequence Checkout

actor Customer "Customer"
participant Web "Web App"
participant Api "Order API"
participant Payment "Payment Service"
participant Db "Database"

Customer -> Web: Checkout
Web -> Api: POST /orders
Api -> Payment: Charge payment
Payment --> Api: Payment accepted
Api -> Db: Save order
Db --> Api: Saved
Api --> Web: 201 Created
Web --> Customer: Confirmation
```

Validate and render it:

```powershell
enzo-diagram validate .\checkout.enzo
enzo-diagram render .\checkout.enzo
enzo-diagram render .\checkout.enzo --format png
```

## Sequence Example

Sequence diagrams support `actor`, `participant`, request messages with `->`, and response messages with `-->`.

```text
sequence Login

actor User "User"
participant Api "API"
participant Db "Database"

User -> Api: Login
Api -> Db: Find user
Db --> Api: User
Api --> User: Success
```

## Flow Example

```text
flow Example

start Begin "Begin"
task Work "Do work"
decision Valid "Valid?"
end Done "Done"
end Failed "Failed"

Begin -> Work
Work -> Valid
Valid -> Done : yes
Valid -> Failed : no
```

## BPMN Example

The BPMN support is a small BPMN-inspired subset, not BPMN 2.0.

```text
bpmn OrderApproval

start Submitted
task Review "Review order"
gateway Approved "Approved?"
task Capture "Capture payment"
end Complete
end Rejected

Submitted -> Review
Review -> Approved
Approved -> Capture : yes
Approved -> Rejected : no
Capture -> Complete
```

## CLI

The NuGet package id is `Enzo.Diagrams.Cli`; the installed command is `enzo-diagram`.

```powershell
dotnet tool install --global Enzo.Diagrams.Cli
enzo-diagram validate .\diagram.enzo
enzo-diagram render .\diagram.enzo
enzo-diagram render .\diagram.enzo --format svg --output .\diagram.svg
enzo-diagram render .\diagram.enzo --format png --output .\diagram.png
```

`render` defaults to SVG and writes next to the source file unless `--output <file>` is provided. Custom output paths must remain inside the source file directory. Invalid input returns a non-zero exit code and writes diagnostics to stderr.

There is no separate `--help` or `--version` option; invalid or incomplete arguments print usage.

## API

Run locally:

```powershell
$env:Enzo__ApiKey = "YOUR_LOCAL_API_KEY"
dotnet run --project src/Enzo.Diagrams.Api --urls http://localhost:5085
```

Protected endpoints:

- `POST /v1/validate`
- `POST /v1/render`

Send the API key as `X-API-Key`.

Validate:

```http
POST http://localhost:5085/v1/validate
X-API-Key: YOUR_LOCAL_API_KEY
Content-Type: application/json

{"source":"flow Demo\nstart Begin \"Begin\"\nend Done \"Done\"\nBegin -> Done"}
```

Render SVG:

```http
POST http://localhost:5085/v1/render
X-API-Key: YOUR_LOCAL_API_KEY
Content-Type: application/json

{"source":"flow Demo\nstart Begin \"Begin\"\nend Done \"Done\"\nBegin -> Done","format":"svg"}
```

For self-hosted deployments that need a temporary PNG URL, use `format: "png"` with `delivery: "url"`. The response includes `id`, `format`, `contentType`, `url`, and `expiresAt`.

See [API authentication](docs/api-authentication.md), [AI integration](docs/ai-integration.md), and the checked-in [OpenAPI contract](docs/openapi/agent.openapi.json).

## Architecture

The current codebase follows Clean Architecture dependency direction:

```text
API / CLI
   |
   v
Application
   |
   v
Domain

Infrastructure implements Application contracts for rendering and temporary artifact storage.
```

- `src/Enzo.Diagrams.Domain` contains DSL models, parsing, validation, and deterministic layout.
- `src/Enzo.Diagrams.Application` contains validation/rendering use cases and outward contracts.
- `src/Enzo.Diagrams.Infrastructure` contains SVG/PNG renderer implementations and render-result storage.
- `src/Enzo.Diagrams.Api` is the ASP.NET Core composition root and HTTP boundary.
- `src/Enzo.Diagrams.Cli` is the command-line composition root.

See [ARCHITECTURE.md](ARCHITECTURE.md) for the full architecture description.

## Sequence benchmark highlights

The final sequence-diagram benchmark used `gpt-4o-mini`, 30 sequence scenarios, 5 runs per scenario, 150 generations per language, temperature `0.2`, up to 3 repair attempts, and deterministic semantic-equivalence scoring with no LLM judge. These results apply to this defined sequence-generation benchmark only.

| What | Enzo result |
| --- | ---: |
| Eventual equivalent validity | **99.3% vs 96.7%** |
| Median tokens to valid diagram | **16.6% lower** |
| Median equivalent-generation cost | **10.8% lower** |
| Simple sequence diagrams | **~12.2% lower TTVED** |
| Medium sequence diagrams | **~16.4% lower TTVED** |
| Generated source size | **~14.0% smaller** |
| Initial prompt/input tokens | **66 fewer on average** |
| Unresolved generations | **1/150 vs 5/150** |

TTV means Tokens to Valid Diagram. TTVED means Tokens to Valid Equivalent Diagram.

Mermaid remained stronger on first-pass validity and had lower mean TTVED for complex sequence workloads. The benchmark therefore does not show that Enzo universally outperforms Mermaid; it shows specific advantages in compactness, median generation cost, and eventual semantic completion for this sequence-diagram workload.

In this benchmark, Enzo was especially effective for simple and medium sequence diagrams. Medium scenarios used about 16% fewer tokens to reach an equivalent valid diagram, while Enzo also produced more compact diagram source and achieved 99.3% eventual semantic equivalence.

Benchmarks are historical and offline by default. Do not rerun live OpenAI benchmarks as part of normal regression validation. See [BENCHMARKS.md](BENCHMARKS.md) for the full methodology, results, failed experiments, repair analysis, and limitations.

## Security / Self-Hosting

The API uses the `X-API-Key` header for protected endpoints. SVG output encodes user-controlled labels and messages, and the API applies request, source, diagram complexity, and PNG rasterization limits.

Enzo Diagrams was previously deployed and tested successfully on Azure Container Apps. The hosted environment was later stopped, and the repository no longer automatically deploys to Azure.

Self-hosting remains supported through the ASP.NET Core API and `src/Enzo.Diagrams.Api/Dockerfile`. The container listens on port `8080` by default.

See [SECURITY.md](SECURITY.md) for reporting and security-sensitive areas.

## Removed And Retired Features

- Azure auto-deployment and active hosted production endpoint.
- MCP server experiment.
- ChatGPT App / Apps SDK experiment.
- Guaranteed inline ChatGPT rendering.
- Interactive web editor or frontend.
- Broad Mermaid replacement positioning.

Historical Azure context remains in [docs/azure-container-deployment.md](docs/azure-container-deployment.md).

## Development

```powershell
dotnet restore
dotnet build
dotnet test
```

Pack the CLI locally without publishing:

```powershell
dotnet pack src/Enzo.Diagrams.Cli\Enzo.Diagrams.Cli.csproj -c Release
```

Build the API container when Docker is available:

```powershell
docker build -f src/Enzo.Diagrams.Api/Dockerfile -t enzo-diagrams-api:local .
```

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md). DSL behavior, rendering output, API contracts, CLI behavior, and security-sensitive rendering paths require regression tests when changed.

## License

Enzo Diagrams is licensed under the [MIT License](LICENSE).
