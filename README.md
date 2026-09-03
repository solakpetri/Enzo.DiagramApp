# Enzo.Diagrams

Enzo.Diagrams is a diagram-as-code engine for humans and AI agents. It defines a small DSL for flowcharts, sequence diagrams, and a BPMN-inspired subset, then renders diagrams as SVG or PNG through a CLI or HTTP API.

## Overview

The repository contains a headless .NET solution. Users write Enzo.Diagrams DSL directly, or ask an AI agent to produce the DSL, then pass it to the parser, validator, layout engine, and renderer.

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

Core boundary: AI does not render the diagram.

AI generates valid Enzo.Diagrams DSL. Enzo.Diagrams parses, validates, lays out, and renders it. The application is AI-provider-independent; any AI agent can use it if it understands the DSL and can call the CLI or HTTP API.

## Supported diagram types

Supported top-level declarations are:

- `flow` for flowcharts
- `sequence` for sequence diagrams
- `bpmn` for a small BPMN-inspired subset

Identifiers must start with an ASCII letter or `_`, followed by ASCII letters, digits, or `_`. Keywords are lowercase and case-sensitive. Quoted labels use double quotes and cannot span multiple lines.

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

Sequence diagrams support `actor` and `participant` declarations. Messages use `->` for synchronous messages and `-->` for responses. Every message requires a label after `:`.

```text
sequence Checkout

actor Customer
participant API
participant Payment

Customer -> API: Checkout
API -> Payment: Charge
Payment --> API: Success
API --> Customer: Confirmed
```

## BPMN subset

The BPMN-inspired subset is not BPMN 2.0 compliant. It supports start events, end events, tasks, exclusive gateways, sequence flows, and optional sequence-flow labels.

```text
bpmn Order

start Received
task Validate "Validate order"
gateway Available "Stock available?"
task Reserve "Reserve stock"
end Complete

Received -> Validate
Validate -> Available
Available -> Reserve : yes
Reserve -> Complete
```

Unsupported BPMN features include pools, lanes, message events, timer events, subprocesses, BPMN XML import/export, full interoperability, and gateway types other than the supported exclusive gateway syntax.

## Installation

The CLI is packaged as a .NET tool. The solution targets .NET `net10.0`, so the .NET 10 SDK is required to build or install local packages.

When `Enzo.Diagrams.Cli` is available from your configured NuGet source, install it globally:

```powershell
dotnet tool install --global Enzo.Diagrams.Cli
```

The installed command is `enzo-diagram`:

```powershell
enzo-diagram validate diagram.enzo
enzo-diagram render diagram.enzo
enzo-diagram render diagram.enzo --format png
```

Build and install a local package into an isolated tool path:

```powershell
dotnet pack src/Enzo.Diagrams.Cli -c Release
dotnet tool install Enzo.Diagrams.Cli --version 0.1.0 --tool-path ./.tools --add-source src/Enzo.Diagrams.Cli/bin/Release
./.tools/enzo-diagram validate examples/checkout.enzo
```

To run from source:

```powershell
git clone https://github.com/solakpetri/Enzo.DiagramApp.git
cd Enzo.DiagramApp
dotnet restore
```

## CLI usage

Run CLI commands from the repository root:

```powershell
dotnet run --project src/Enzo.Diagrams.Cli -- validate examples/checkout.enzo
dotnet run --project src/Enzo.Diagrams.Cli -- render examples/checkout.enzo --format svg
dotnet run --project src/Enzo.Diagrams.Cli -- render examples/checkout.enzo --format png
dotnet run --project src/Enzo.Diagrams.Cli -- render examples/checkout.enzo --output examples/checkout-custom.svg
```

If `--output` is omitted, the CLI writes next to the source file with the selected format extension. Custom output paths must include a file name and stay inside the source file directory.

## Validation

Create a diagram file manually:

```powershell
New-Item -ItemType Directory -Force -Path examples
@'
flow Checkout

start Begin "Order received"
task Validate "Validate order"
end Complete "Complete order"

Begin -> Validate
Validate -> Complete
'@ | Set-Content -Encoding UTF8 examples/checkout.enzo
```

Validate it:

```powershell
dotnet run --project src/Enzo.Diagrams.Cli -- validate examples/checkout.enzo
```

A valid file prints `Valid: <file>`. Invalid files return a non-zero exit code and print syntax or validation errors with line and column information.

Validation currently checks syntax, duplicate identifiers, unknown references, required flowchart/BPMN start and end elements, and cycles in flowchart/BPMN graphs.

## Rendering

Rendering parses and validates first. Invalid diagrams are not rendered.

## SVG output

Render SVG to the default output path:

```powershell
dotnet run --project src/Enzo.Diagrams.Cli -- render examples/checkout.enzo --format svg
```

Choose an SVG output path:

```powershell
dotnet run --project src/Enzo.Diagrams.Cli -- render examples/checkout.enzo --format svg --output examples/checkout-public.svg
```

## PNG output

PNG output is produced by rasterizing the generated SVG with Svg.Skia/SkiaSharp.

```powershell
dotnet run --project src/Enzo.Diagrams.Cli -- render examples/checkout.enzo --format png
dotnet run --project src/Enzo.Diagrams.Cli -- render examples/checkout.enzo --format png --output examples/checkout-public.png
```

## HTTP API

Start the API:

```powershell
dotnet run --project src/Enzo.Diagrams.Api --urls http://localhost:5085
```

Call the validation API from another terminal:

```powershell
[string]$source = Get-Content examples/checkout.enzo -Raw
Invoke-RestMethod -Uri http://localhost:5085/v1/validate -Method Post -ContentType 'application/json' -Body (@{ source = $source } | ConvertTo-Json)
```

Render SVG through the API:

```powershell
[string]$source = Get-Content examples/checkout.enzo -Raw
Invoke-WebRequest -UseBasicParsing -Uri http://localhost:5085/v1/render -Method Post -ContentType 'application/json' -Body (@{ source = $source; format = 'svg' } | ConvertTo-Json) -OutFile examples/checkout-api.svg
```

Render PNG through the API:

```powershell
[string]$source = Get-Content examples/checkout.enzo -Raw
Invoke-WebRequest -UseBasicParsing -Uri http://localhost:5085/v1/render -Method Post -ContentType 'application/json' -Body (@{ source = $source; format = 'png' } | ConvertTo-Json) -OutFile examples/checkout-api.png
```

Endpoints:

- `POST /v1/validate` accepts `{ "source": "..." }` and returns `{ "valid": true }` for valid DSL.
- `POST /v1/render` accepts `{ "source": "...", "format": "svg" }` or `{ "source": "...", "format": "png" }` and returns `image/svg+xml` or `image/png`.
- Invalid requests return `application/problem+json` with an `errors` extension.

## Docker

Build the API image from the repository root:

```powershell
docker build -f src/Enzo.Diagrams.Api/Dockerfile -t enzo-diagrams-api .
```

Run the API at `http://localhost:5085`:

```powershell
docker run --rm -p 5085:8080 enzo-diagrams-api
```

The container listens on HTTP port `8080`.

## AI / ChatGPT integration

The supported integration model is prompt-based DSL generation plus CLI/API rendering. A ChatGPT conversation, custom GPT, coding agent, or automation script can generate Enzo.Diagrams DSL and send it to Enzo.Diagrams for validation and rendering.

No packaged ChatGPT integration is currently configured in this repository. A packaged integration could be added later, but the core application does not depend on a specific AI provider.

See [docs/ai-integration.md](docs/ai-integration.md) for the agent-facing API contract, integration algorithm, DSL examples, and current limitations.

## AI agent instructions

When generating diagrams for Enzo.Diagrams:

- Use one diagram type: `flow`, `sequence`, or `bpmn`.
- Produce syntactically valid DSL only; do not produce SVG, PNG, Mermaid, PlantUML, or BPMN XML.
- Use valid identifiers: ASCII letter or `_` first, then ASCII letters, digits, or `_`.
- For `flow`, declare `start`, `task`, `decision`, or `end` nodes as `<kind> <Id> "Label"`, then connect them with `<From> -> <To>` and optional `: label`.
- For `sequence`, declare `actor <Id>` or `participant <Id>`, then messages as `<From> -> <To>: Label` or `<From> --> <To>: Label`.
- For `bpmn`, declare `start <Id>`, `task <Id> "Label"`, `gateway <Id> "Label"`, or `end <Id>`, then sequence flows as `<From> -> <To>` and optional `: label`.
- Send the DSL to Enzo.Diagrams through the CLI or HTTP API for validation and rendering.

## Development setup

```powershell
dotnet restore
```

Use the CLI and API startup commands above for local development.

## Build instructions

```powershell
dotnet build
```

## Test instructions

```powershell
dotnet test
```

## Repository structure

```text
.github/workflows/code-review.yml        Pull-request code review workflow
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
- Packaged CLI releases.
- A documented AI action/plugin wrapper around the HTTP API.
- More layout controls.
- DSL comments and richer label handling.

## License

No license file is currently present in this repository. Until a license is added, do not assume open-source usage rights beyond viewing the public repository.
