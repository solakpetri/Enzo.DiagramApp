# Enzo vs Mermaid Token Benchmark

This benchmark compares deterministic source representations for equivalent Enzo.Diagrams DSL and Mermaid diagrams. It answers whether the Enzo DSL requires fewer tokens than Mermaid for the committed fixture set.

The benchmark does not assume Enzo wins. Results are calculated from committed scenarios every time the runner executes.

## Why Tokens Matter

AI systems that generate diagrams pay for and are constrained by tokens. A representation that needs fewer output tokens may fit more easily in model context and may cost less to generate. This first benchmark measures only DSL representation/token efficiency, not total LLM generation cost.

Future benchmarks may measure prompt/context tokens, generated output tokens, first-pass validity, repair attempts, and total tokens until valid render.

## Dataset

Fixtures live under `benchmarks/scenarios` and are committed as JSON:

| Category | Count |
| --- | ---: |
| Flow | 10 |
| Sequence | 10 |
| Process/business workflow | 10 |

Each scenario contains a natural-language prompt, an Enzo DSL implementation, and a materially equivalent Mermaid implementation. The set includes simple, medium, and complex diagrams, including larger scenarios for scaling observations.

## Methodology

For each Enzo and Mermaid source, the runner measures UTF-8 bytes, character count, non-empty line count, and token count. It also derives tokens per element, tokens per connection/message, and Enzo token difference versus Mermaid.

Token counts use `SharpToken` with the OpenAI-compatible `cl100k_base` encoding by default. Configure the encoding with `--encoding=<name>`:

```powershell
dotnet run --project benchmarks/Enzo.Diagrams.Benchmarks -- --encoding=cl100k_base
```

Do not estimate token counts from character counts; the runner tokenizes both representations with the same tokenizer.

## Equivalence

The benchmark checks semantic structure, not visual styling or layout syntax. It compares diagram kind, element/participant count, connection/message count, element labels, normalized connections/messages, message labels, and labeled decision branches.

Scenarios that fail structural equivalence are rejected. This prevents silently benchmarking diagrams that do not represent the same semantics.

## Validation

Enzo fixtures are parsed and validated with the real `Enzo.Diagrams.Language` parser/validator. The runner also renders each valid Enzo fixture through `Enzo.Diagrams.Rendering` to verify it is usable by the renderer.

Mermaid fixtures are validated by a deterministic subset parser for the Mermaid syntax used in the fixtures: `flowchart TD` nodes/edges for flow and process scenarios, and `sequenceDiagram` participants/messages for sequence scenarios. Unsupported or inconsistent Mermaid lines fail the benchmark.

Optional Mermaid render validation can be run separately with Mermaid CLI if desired:

```powershell
npm install -g @mermaid-js/mermaid-cli
mmdc -i path/to/fixture.mmd -o out.svg
```

Mermaid CLI is not required by the core .NET benchmark to keep execution lightweight and deterministic.

## Running

```powershell
dotnet run --project benchmarks/Enzo.Diagrams.Benchmarks
```

The command loads scenarios, validates Enzo and Mermaid fixtures, checks structural equivalence, calculates metrics, prints a summary, and writes results to `benchmarks/results`.

Outputs:

| File | Purpose |
| --- | --- |
| `benchmarks/results/enzo-vs-mermaid-results.json` | Machine-readable scenario metrics |
| `benchmarks/results/enzo-vs-mermaid-summary.md` | Human-readable summary and scaling table |

## Interpreting Results

Positive token difference means Enzo used fewer tokens than Mermaid. Negative token difference means Enzo used more tokens. The Markdown report phrases the direction explicitly, for example `Enzo uses 24.3% fewer tokens than Mermaid` or `Enzo uses 8.1% more tokens than Mermaid`.

Do not treat this benchmark as evidence about visual quality, Mermaid rendering compatibility beyond the validated subset, AI first-pass generation reliability, or total model cost.

## Fairness Rules

Fixtures should not intentionally make Mermaid verbose, shorten Enzo at the cost of semantics, include decorative Mermaid syntax Enzo cannot represent, exclude cases where Mermaid performs better, manually alter results, or cherry-pick favorable categories.
