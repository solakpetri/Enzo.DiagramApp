# Enzo vs Mermaid Benchmarks

These benchmarks compare Enzo.Diagrams DSL and Mermaid using the same committed scenario set. They exist to test hypotheses, not to prove that Enzo is better. All results must be reported, including categories where Mermaid wins.

## Benchmark 1: Static DSL Representation

This benchmark compares deterministic source representations for equivalent Enzo.Diagrams DSL and Mermaid diagrams. It answers whether the Enzo DSL requires fewer tokens than Mermaid for the committed fixture set.

The benchmark does not assume Enzo wins. Results are calculated from committed scenarios every time the runner executes.

## Why Tokens Matter

AI systems that generate diagrams pay for and are constrained by tokens. A representation that needs fewer output tokens may fit more easily in model context and may cost less to generate. This first benchmark measures only DSL representation/token efficiency, not total LLM generation cost.

This first benchmark measures only committed DSL representation size. It does not measure prompt/context tokens, generated output tokens, first-pass validity, repair attempts, or total tokens until valid render.

Current committed representation results:

| Metric | Enzo | Mermaid |
| --- | ---: | ---: |
| Total DSL tokens | 4465 | 4410 |

Overall, Enzo uses 1.2% more tokens in the static fixtures. Enzo sequence diagrams use 8.8% fewer tokens.

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

## Benchmark 2: LLM Generation Cost

The LLM benchmark measures the actual token cost of going from the same natural-language request to a valid, equivalent diagram. The primary metric is now Tokens to Valid Equivalent Diagram (TTVED). Tokens to Valid Diagram (TTV) remains a diagnostic metric for syntax/render success.

It reuses the exact 30 scenarios under `benchmarks/scenarios`. It does not rewrite prompts, cherry-pick scenarios, or optimize the Enzo DSL.

### Methodology

For each scenario, the runner independently asks the same model to generate Enzo.Diagrams DSL and Mermaid source using fixed generation settings. Defaults are 30 scenarios, 5 Enzo runs per scenario, and 5 Mermaid runs per scenario, for 300 generations.

Each initial generation records prompt/input tokens, output tokens, total tokens, duration, first-pass validity, Markdown fence normalization, and validation/render result. If the output is invalid, the runner asks the same model to repair the diagram using the invalid source and validation error. Repair attempts are capped by `--max-repair-attempts`, defaulting to 3.

Valid Diagram is defined as a diagram that parses/validates and renders successfully.

Tokens to Valid Diagram is defined as:

```text
initial input tokens + initial output tokens + all repair input tokens + all repair output tokens
```

Repair prompt context is included. Cold-start input token cost is reported separately because Enzo requires DSL guidance and Mermaid may already be familiar to the model. Generation-only output/repair tokens are also reported, but they are not total API cost.

Valid Equivalent Diagram is defined as a Valid Diagram that also uses the expected benchmark kind, contains required scenario concepts, and meets minimum structural expectations. Tokens to Valid Equivalent Diagram is the token total through the first equivalent-valid attempt. If no attempt reaches equivalence, TTVED is reported as unresolved rather than assigning a fabricated finite value.

Semantic equivalence is deterministic and benchmark-specific. It does not prove perfect semantic equivalence. It prevents obvious benchmark failures such as a sequence request producing a flowchart, a complex workflow producing a tiny linear diagram, or a required participant/system being omitted.

### Diagram Kinds

Each committed scenario declares one expected benchmark kind:

| Benchmark kind | Enzo accepted kind | Mermaid accepted representation |
| --- | --- | --- |
| `flow` | `flow` | `flowchart` or `graph` |
| `sequence` | `sequence` | `sequenceDiagram` |
| `process` | `bpmn` | `flowchart` or `graph` |

Mermaid process scenarios intentionally use `flowchart`/`graph` as the business-process representation because Mermaid has no BPMN representation in the current benchmark. A Mermaid flowchart only maps to `process` when the scenario expected kind is `process`; a sequence scenario that generates a flowchart remains wrong-kind and fails equivalence.

### Semantic Checks

Each scenario includes deterministic structural expectations such as required concepts, minimum node/edge/decision counts for flow/process diagrams, or minimum participant/interaction counts for sequence diagrams.

Generated Enzo source is converted into benchmark facts using the real Enzo parser/AST. Generated Mermaid source keeps real Mermaid CLI render validation; benchmark facts are extracted by a narrow, deterministic adapter for the supported benchmark forms only. Semantic validation operates on language-neutral facts: kind, labels, node/edge counts, decisions, participants, and interactions.

Concept matching normalizes labels and identifiers by lowercasing, splitting common identifier casing, removing punctuation, and normalizing whitespace. Explicit scenario aliases are used where necessary. The matcher is intentionally not an LLM judge and does not use broad fuzzy matching.

### Fairness Rules

Both languages receive the same natural-language scenario, model, temperature, top-p, and output token limit. Prompts are stored in `benchmarks/Enzo.Diagrams.LlmBenchmarks/prompts` for auditing. The prompts do not mention the comparison, benchmark results, or token-count optimization.

Do not exclude failed Enzo runs, successful Mermaid runs, or categories where Mermaid wins. Do not manually edit generated benchmark results.

### API And Configuration

The runner uses the OpenAI API and requires `OPENAI_API_KEY`. The key is read from the environment only, is never printed, and is not persisted in results.

The model is configurable with `--model`, `ENZO_LLM_BENCHMARK_MODEL`, or `OPENAI_MODEL`. Generation settings are configurable with `--temperature`, `--top-p`, and `--max-output-tokens`.

### Validation

Enzo output is validated with the real `Enzo.Diagrams.Language` parser and rendered through `Enzo.Diagrams.Rendering`.

Mermaid output is validated by rendering with Mermaid CLI. Install Node.js and Mermaid CLI before running Mermaid benchmarks:

```powershell
npm install -g @mermaid-js/mermaid-cli
```

Set a custom Mermaid CLI path with `--mermaid-command` or `MERMAID_CLI` if `mmdc` is not on `PATH`.

Each generation result records `syntaxValid`, `renderValid`, `kindValid`, `structureValid`, `semanticValid`, `equivalentValid`, failure reasons, and semantic diagnostics such as expected/actual kind, matched/missing concepts, and applicable structural counts.

### Commands

Cheap smoke test:

```powershell
dotnet run --project benchmarks/Enzo.Diagrams.LlmBenchmarks -- --runs 1
```

Full default run:

```powershell
dotnet run --project benchmarks/Enzo.Diagrams.LlmBenchmarks -- --runs 5
```

Useful filters:

```powershell
dotnet run --project benchmarks/Enzo.Diagrams.LlmBenchmarks -- --runs 1 --category sequence --language enzo
dotnet run --project benchmarks/Enzo.Diagrams.LlmBenchmarks -- --scenario flow-login-basic --model gpt-4o-mini
```

Manual one-run baseline after reviewing offline re-evaluation:

```powershell
dotnet run --project benchmarks/Enzo.Diagrams.LlmBenchmarks -- --runs 1 --language all
```

This runs the frozen 30 scenarios once for Enzo and once for Mermaid: 60 initial generations.

Resume an interrupted run:

```powershell
dotnet run --project benchmarks/Enzo.Diagrams.LlmBenchmarks -- --resume benchmarks/results/llm-generation-cost/<run>.json
```

Offline semantic re-evaluation of a persisted run:

```powershell
dotnet run --project benchmarks/Enzo.Diagrams.LlmBenchmarks -- --reevaluate benchmarks/results/llm-generation-cost/<run>.json
```

This mode requires no OpenAI API key, makes no OpenAI calls, does not regenerate diagrams, and does not modify the historical source result. It writes separate `*-semantic.json`, `*-semantic.csv`, and `*-semantic.md` files next to the original run.

### Results

Outputs are written under `benchmarks/results/llm-generation-cost` with timestamped filenames so historical runs are not overwritten. The runner writes JSON, CSV, and Markdown after each completed scenario/language/run combination so partial data survives interruptions.

### Limitations

LLM generation is stochastic. Reports include mean, median, min, max, and standard deviation where practical, but the benchmark does not claim statistical significance. Cost estimates are intentionally excluded unless pricing is supplied in future work because model pricing changes over time.
