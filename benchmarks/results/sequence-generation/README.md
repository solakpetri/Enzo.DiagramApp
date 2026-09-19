# Sequence Generation Benchmark

## Hypothesis

Enzo may require fewer total tokens than Mermaid `sequenceDiagram` to produce a valid equivalent sequence diagram while maintaining the same or near-equivalent reliability.

Primary metric: Tokens to Valid Equivalent Diagram (TTVED).

## Why Sequence

Earlier broad benchmarks showed a sequence-specific signal while flow and process generation remained noisy. Static representation results showed Enzo sequence syntax at about 8.8% fewer tokens than Mermaid. One prior equivalent-generation observation had Enzo 10/10, Mermaid 10/10, Enzo TTVED 507.9, and Mermaid TTVED 522.1. These were small samples and only motivate this dedicated benchmark.

## Scenario Design

The suite contains 30 deterministic sequence scenarios: 10 simple, 10 medium, and 10 complex. The original frozen sequence scenarios are copied into `benchmarks/scenarios/sequence-generation/existing-baseline.scenarios.json`. New sequence-specific scenarios are split by complexity in `new-simple.scenarios.json`, `new-medium.scenarios.json`, and `new-complex.scenarios.json`.

Scenarios cover realistic software and integration workflows: login, cache lookup, uploads, database reads, webhooks, token validation, payment, CRUD, OAuth, inventory, retries, service authentication, jobs, approval, orchestration, event processing, fraud checks, identity-provider login, async callbacks, and platform synchronization.

## Equivalence Rules

A successful run must be syntax valid, render valid, the expected sequence kind, structurally complete, and semantically complete. The validator checks required concepts, required participants, minimum participant count, minimum interaction count, and configured required interactions. It does not require exact source matching.

Participant and interaction matching uses deterministic normalization so forms such as `Order API`, `OrderApi`, `order-api`, and `order api` can match. Scenario aliases are explicit when useful. No LLM judge is used.

## Token Accounting

TTV remains the total input plus output tokens through the first syntax/render-valid diagram. TTVED remains the total input plus output tokens through the first equivalent-valid diagram. For valid-equivalent results, reports also include source characters, UTF-8 bytes, non-empty lines, output tokens, participants, interactions, output tokens per participant, output tokens per interaction, and TTVED per interaction.

## Configuration

Default sequence benchmark configuration:

```text
Model: gpt-4o-mini
Temperature: 0.2
Max repair attempts: 3
Runs: 1 for the initial exploratory run
```

The same settings are used for Enzo and Mermaid. Normal tests are offline and do not require `OPENAI_API_KEY`.

## Methodology

The initial benchmark is exploratory: 30 scenarios x 1 run x 2 languages, for 60 initial generations plus repairs. Do not treat it as a public claim.

The confirmation benchmark standard is 30 scenarios x 5 runs, producing 150 Enzo generations and 150 Mermaid generations plus repairs. Persisted JSON, CSV, and Markdown files keep scenario/run-level data.

## Success Thresholds

Initial signal is promising if Enzo eventual equivalent-valid is at least 95% and Enzo successful TTVED is less than or equal to Mermaid successful TTVED. A stronger signal is at least 10% lower Enzo TTVED. A long-term stretch target is at least 20% lower Enzo TTVED while maintaining at least 99% eventual equivalent-valid and first-pass equivalent validity within 1-2 percentage points of Mermaid.

## Public Claims

Do not publicly claim a sequence cost advantage until the 30 x 5 confirmation benchmark is complete and both languages reach at least 95% eventual equivalent-valid, preferably 99%. If reliability differs materially, report cost alongside reliability rather than as a standalone percentage.

## Manual Command

```powershell
$env:OPENAI_API_KEY = "your-openai-api-key"

dotnet run --project benchmarks/Enzo.Diagrams.LlmBenchmarks -- --suite sequence --runs 1
```

Results are written to `benchmarks/results/sequence-generation/` as JSON, CSV, and Markdown.
