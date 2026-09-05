# Enzo.Diagrams

Enzo.Diagrams is a headless diagram-as-code engine designed for humans and AI agents. AI can generate a compact Enzo.Diagrams DSL; the engine parses, validates, lays out, and renders it as SVG or PNG through a CLI or HTTP API.

## Overview

The repository contains a .NET solution with a custom DSL, lexer/parser, semantic validation, deterministic layout, SVG rendering, PNG conversion, a command-line tool, and a minimal HTTP API.

Users can write DSL directly, or ask an AI agent to produce it, then pass the DSL to Enzo.Diagrams for validation and rendering.

The project currently has no interactive frontend, database, packaged SDK, or packaged ChatGPT plugin.

## Why this project exists

Diagramming tools are often visual-first. Enzo.Diagrams takes the opposite approach: keep diagrams as plain text so they can be reviewed, versioned, generated, validated, and rendered by automation.

This is especially useful for AI-assisted workflows because the AI only has to produce a small, structured language. Rendering remains application logic owned by Enzo.Diagrams.

## Architecture

```text
Natural language
      ↓
     AI
      ↓
Enzo.Diagrams DSL
      ↓
    Parser
      ↓
  Validator
      ↓
    Layout
      ↓
     SVG
      ↓
 optional PNG
```

Architectural boundary: AI generates the DSL. Enzo.Diagrams renders the diagram.

AI generates valid Enzo.Diagrams DSL. Enzo.Diagrams parses, validates, lays out, and renders it. The application is AI-provider-independent; any AI agent can use it if it understands the DSL and can call the CLI or HTTP API.

## Supported diagrams

Supported top-level declarations are:

- `flow` for flowcharts
- `sequence` for sequence diagrams
- `bpmn` for a small BPMN-inspired subset

Identifiers must start with an ASCII letter or `_`, followed by ASCII letters, digits, or `_`. Keywords are lowercase and case-sensitive. Quoted labels use double quotes and cannot span multiple lines.

## Quick start

When `Enzo.Diagrams.Cli` is available from your configured NuGet source, install it globally:

```bash
dotnet tool install --global Enzo.Diagrams.Cli
```

Validate and render an included example:

```bash
enzo-diagram validate examples/flowchart/order-fulfillment.enzo
enzo-diagram render examples/flowchart/order-fulfillment.enzo
enzo-diagram render examples/flowchart/order-fulfillment.enzo --format png
```

The CLI command is `enzo-diagram`. `render` defaults to SVG and writes next to the source file unless `--output <file>` is provided.

## Flowchart DSL

Flowcharts support `start`, `task`, `decision`, and `end` nodes. Each node requires an identifier and quoted label. Edges use `->` and may include an optional label after `:`.

```text
flow Checkout

start Begin "Order received"
task Validate "Validate order"
decision Available "Stock available?"
task Reserve "Reserve stock"
end Complete "Complete order"
end Reject "Reject order"

Begin -> Validate
Validate -> Available
Available -> Reserve : yes
Available -> Reject : no
Reserve -> Complete
```

## Sequence diagram DSL

Sequence diagrams support `actor` and `participant` declarations, with optional quoted display names. Messages use `->` for synchronous messages and `-->` for responses. Every message requires a label after `:`.

```text
sequence OrderPayment

actor Customer "Customer"
participant Storefront "Storefront"
participant Payments "Payment Service"

Customer -> Storefront: Create order
Storefront -> Payments: POST /payments
Payments --> Storefront: 200 OK
Storefront --> Customer: Order paid
```

## BPMN subset

The BPMN-inspired subset is not BPMN 2.0 compliant. It supports start events, end events, tasks, exclusive gateways, sequence flows, and optional sequence-flow labels. Start and end events use their identifiers as labels.

```text
bpmn Order

start Received
task Validate "Validate order"
gateway Available "Stock available?"
task Reserve "Reserve stock"
end Complete
end Rejected

Received -> Validate
Validate -> Available
Available -> Reserve : yes
Available -> Rejected : no
Reserve -> Complete
```

Unsupported BPMN features include pools, lanes, message events, timer events, subprocesses, BPMN XML import/export, full interoperability, and gateway types other than the supported exclusive gateway syntax.

## Installation

The CLI is packaged as a .NET tool. The solution targets .NET `net10.0`, so the .NET 10 SDK is required to build or install local packages.

Global installation uses the configured NuGet sources:

```bash
dotnet tool install --global Enzo.Diagrams.Cli
```

Build and install a local package into an isolated tool path:

```bash
dotnet pack src/Enzo.Diagrams.Cli -c Release
dotnet tool install Enzo.Diagrams.Cli --version 0.1.0 --tool-path ./.tools --add-source src/Enzo.Diagrams.Cli/bin/Release
./.tools/enzo-diagram validate examples/flowchart/order-fulfillment.enzo
```

To run from source:

```bash
git clone https://github.com/solakpetri/Enzo.DiagramApp.git
cd Enzo.DiagramApp
dotnet restore
```

## CLI usage

Run CLI commands from the repository root:

```bash
dotnet run --project src/Enzo.Diagrams.Cli -- validate examples/flowchart/order-fulfillment.enzo
dotnet run --project src/Enzo.Diagrams.Cli -- render examples/flowchart/order-fulfillment.enzo --format svg
dotnet run --project src/Enzo.Diagrams.Cli -- render examples/flowchart/order-fulfillment.enzo --format png
dotnet run --project src/Enzo.Diagrams.Cli -- render examples/flowchart/order-fulfillment.enzo --output examples/flowchart/order-fulfillment-custom.svg
```

If `--output` is omitted, the CLI writes next to the source file with the selected format extension. Custom output paths must include a file name and stay inside the source file directory.

## Examples directory

```text
examples/
  flowchart/order-fulfillment.enzo
  sequence/order-payment.enzo
  bpmn/order-approval.enzo
```

The examples are source DSL files only. Generated SVG and PNG files are intentionally not committed.

## Validation

Validate it:

```bash
dotnet run --project src/Enzo.Diagrams.Cli -- validate examples/flowchart/order-fulfillment.enzo
```

A valid file prints `Valid: <file>`. Invalid files return a non-zero exit code and print syntax or validation errors with line and column information.

Validation currently checks syntax, duplicate identifiers, unknown references, required flowchart/BPMN start and end elements, and cycles in flowchart/BPMN graphs.

## Rendering

Rendering parses and validates first. Invalid diagrams are not rendered.

## SVG output

Render SVG to the default output path:

```bash
dotnet run --project src/Enzo.Diagrams.Cli -- render examples/flowchart/order-fulfillment.enzo --format svg
```

Choose an SVG output path:

```bash
dotnet run --project src/Enzo.Diagrams.Cli -- render examples/flowchart/order-fulfillment.enzo --format svg --output examples/flowchart/order-fulfillment-public.svg
```

## PNG output

PNG output is produced by rasterizing the generated SVG with Svg.Skia/SkiaSharp.

```bash
dotnet run --project src/Enzo.Diagrams.Cli -- render examples/flowchart/order-fulfillment.enzo --format png
dotnet run --project src/Enzo.Diagrams.Cli -- render examples/flowchart/order-fulfillment.enzo --format png --output examples/flowchart/order-fulfillment-public.png
```

## HTTP API

Start the API:

```bash
dotnet run --project src/Enzo.Diagrams.Api --urls http://localhost:5085
```

Render SVG through the API:

```bash
curl -s http://localhost:5085/v1/render \
  -H "Content-Type: application/json" \
  -H "X-API-Key: $ENZO_API_KEY" \
  -d '{"source":"sequence Order\nactor Customer\nparticipant Storefront\nCustomer -> Storefront: Create order","format":"svg"}' \
  -o order.svg
```

Validation request shape:

```json
{
  "source": "flow Checkout\nstart Begin \"Order received\"\nend Complete \"Complete order\"\nBegin -> Complete"
}
```

Render request shape:

```json
{
  "source": "flow Checkout\nstart Begin \"Order received\"\nend Complete \"Complete order\"\nBegin -> Complete",
  "format": "svg"
}
```

Endpoints:

- `POST /v1/validate` accepts `{ "source": "..." }` and returns `{ "valid": true }` for valid DSL.
- `POST /v1/render` accepts `{ "source": "...", "format": "svg" }` or `{ "source": "...", "format": "png" }` and returns `image/svg+xml` or `image/png`.
- Invalid requests return `application/problem+json` with an `errors` extension.

## API security

Hosted API requests to `POST /v1/validate` and `POST /v1/render` require the `X-API-Key` request header. The key is configured with `Enzo:ApiKey`, or `Enzo__ApiKey` as an environment variable. Do not commit real API keys.

Generated OpenAPI at `GET /openapi/v1.json` remains public for integration discovery. See [docs/api-authentication.md](docs/api-authentication.md) for Azure Container Apps configuration and [docs/ai-integration.md](docs/ai-integration.md) for ChatGPT Action setup.

## Docker usage

Build the API image from the repository root:

```bash
docker build -f src/Enzo.Diagrams.Api/Dockerfile -t enzo-diagrams-api .
```

Run the API at `http://localhost:5085`:

```bash
docker run --rm -p 5085:8080 enzo-diagrams-api
```

The container listens on HTTP port `8080`.

## AI integration

Enzo.Diagrams supports AI-agent workflows without depending on an AI provider or SDK. Agents generate Enzo.Diagrams DSL, validate it, correct validation errors if needed, then render deterministic SVG or PNG through the API.

```text
Natural language
       ↓
      AI
       ↓
Enzo.Diagrams DSL
       ↓
 /v1/validate
       ↓
  /v1/render
       ↓
      SVG
```

See [docs/ai-integration.md](docs/ai-integration.md) for custom-agent instructions, ChatGPT Action setup, DSL examples, and current limitations. Use the checked-in agent OpenAPI contract at [docs/openapi/agent.openapi.json](docs/openapi/agent.openapi.json).

## Development

```bash
dotnet restore
```

Use the CLI and API startup commands above for local development.

## Build

```bash
dotnet build
```

## Tests

```bash
dotnet test
```

## Release process

CLI releases are tag-driven. Push a semantic version tag in `vMAJOR.MINOR.PATCH` format, such as `v0.1.0`, to run `.github/workflows/release-cli.yml`.

The release workflow restores, builds, tests, packs `Enzo.Diagrams.Cli`, derives the NuGet package version from the tag without the leading `v`, and publishes the package to NuGet.org using NuGet Trusted Publishing via GitHub OIDC.

No long-lived NuGet API key is required. The workflow authenticates with NuGet.org using the GitHub OIDC identity and a temporary credential generated by `NuGet/login@v1`.

Maintainers must configure the following in the GitHub repository under **Settings → Secrets and variables → Actions → Variables**:

| Variable | Value |
| --- | --- |
| `NUGET_USER` | The NuGet.org username associated with the Trusted Publishing policy |

The Trusted Publishing policy on NuGet.org must reference the same GitHub organization/user, repository, and workflow filename (`.github/workflows/release-cli.yml`).

The workflow requires the `id-token: write` permission to request an OIDC token.

Basic release steps:

```bash
git tag v0.1.0
git push origin v0.1.0
```

## Repository structure

```text
.github/workflows/code-review.yml        Pull-request code review workflow
.github/workflows/release-cli.yml        Tagged CLI release workflow
examples/flowchart/                      Flowchart DSL examples
examples/sequence/                       Sequence diagram DSL examples
examples/bpmn/                           BPMN subset DSL examples
src/Enzo.Diagrams.Api/                  Minimal HTTP API
src/Enzo.Diagrams.Cli/                  Command-line entry point
src/Enzo.Diagrams.Core/                 Core project currently present in the solution
src/Enzo.Diagrams.Language/             Lexer, parsers, validators, and layout models
src/Enzo.Diagrams.Rendering/            SVG renderers and PNG rasterization
tests/Enzo.Diagrams.Api.Tests/          HTTP API tests
tests/Enzo.Diagrams.Cli.Tests/          CLI behavior tests
tests/Enzo.Diagrams.Language.Tests/     DSL parsing, validation, and layout tests
tests/Enzo.Diagrams.Rendering.Tests/    Rendering tests
```

## Code review workflow

Pull requests trigger `Enzo.Helpers.CodeReviewer` when the PR is opened. The workflow is defined in `.github/workflows/code-review.yml` and calls `solakpetri/Enzo.Helpers.CodeReviewer/.github/workflows/review.yml@main`.

Maintainers configure the reviewer by adding `OPENAI_API_KEY` as a GitHub Actions repository secret. The secret is used only by the code-review workflow, must never be committed, and is not required by the Enzo.Diagrams application to parse, validate, or render diagrams.

## Contributing

Contributions should keep the DSL small, validate input before rendering, and avoid coupling the renderer to any AI provider. Include tests for parser, validator, CLI, API, or renderer behavior as appropriate.

## Limitations

- No interactive frontend.
- No packaged SDK or hosted service.
- No packaged ChatGPT/custom GPT integration.
- No comment syntax in the DSL.
- No escaped quotes or multiline string labels.
- BPMN support is a small inspired subset, not full BPMN 2.0.
- CLI custom output paths are restricted to the source file directory.

## Roadmap

Potential future work:

- Broader BPMN coverage.
- Published package availability and versioned documentation.
- A documented AI action/plugin wrapper around the HTTP API.
- More layout controls.
- DSL comments and richer label handling.

## License status

No license file is currently present in this repository. Until a license is added, do not assume open-source usage rights beyond viewing the public repository.
