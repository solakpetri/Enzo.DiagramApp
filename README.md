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

This project is currently under development.
