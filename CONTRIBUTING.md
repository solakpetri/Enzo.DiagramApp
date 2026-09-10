# Contributing

## Build And Test

```powershell
dotnet restore
dotnet build
dotnet test
```

The full test suite must run without Azure, OpenAI, or other live external services.

## Branches And Commits

Use focused branches and conventional commit messages such as `fix:`, `test:`, `docs:`, and `chore:`. Keep unrelated cleanup separate from behavior changes.

## DSL Changes

Do not change Enzo DSL behavior casually. DSL changes should include parser and validator tests, renderer coverage when rendering is affected, and documentation updates for examples or syntax.

Preserve the existing language boundaries: flow diagrams, sequence diagrams, and the lightweight BPMN-inspired subset.

## Rendering And Security

User-controlled text flows from DSL source into SVG and PNG output. Changes in parsing, layout, SVG generation, PNG rasterization, API responses, or hosted render storage should consider injection, request-size abuse, diagram complexity, and error disclosure.

Add regression tests for security-sensitive rendering behavior, especially SVG escaping and oversized input rejection.

## Benchmarks

Benchmark reports under `benchmarks/results/` are historical evidence. Do not delete failed or unfavorable results merely because they show regressions. Do not make live LLM calls in ordinary build/test validation.
