# Enzo.Diagrams

Enzo.Diagrams is a headless diagram-as-code engine written in .NET.

Users provide a custom DSL directly through the CLI/API or through an AI client such as ChatGPT. The engine will parse, validate, lay out, and render diagrams as SVG or PNG.

## Architecture

- `Enzo.Diagrams.Core`: shared domain primitives and engine contracts.
- `Enzo.Diagrams.Language`: DSL parsing and validation.
- `Enzo.Diagrams.Rendering`: diagram layout and SVG/PNG rendering.
- `Enzo.Diagrams.Cli`: command-line entry point.
- `Enzo.Diagrams.Api`: HTTP API entry point.

The project has no interactive frontend and no database.

## Diagram DSL

Supported diagram declarations are `flow`, `sequence`, and a deliberately small `bpmn` subset.

The BPMN-inspired subset is not full BPMN 2.0 compliance. It supports only start events, end events, tasks, exclusive gateways, sequence flows, and sequence flow labels:

```text
bpmn Order
start Received
task Validate "Validate order"
gateway Available "Stock available?"
end Complete
Received -> Validate
Validate -> Available
Available -> Complete : yes
```

The BPMN subset does not support pools, lanes, message events, timer events, subprocesses, BPMN XML, full interoperability, or additional gateway types.

This project is currently under development.
