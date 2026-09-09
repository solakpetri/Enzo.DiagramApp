# Sequence Completeness Optimization

## Hypothesis

Enzo sequence generation is already compact enough to compete with Mermaid, but medium and complex sequence diagrams enter repair too often because the model omits required participants, concepts, or interactions on the first pass.

## Baseline

Latest dedicated sequence benchmark: `sequence-generation-20260909-183712-453-gpt-4o-mini`.

Configuration:

```text
Model: gpt-4o-mini
Scenarios: 30
Runs per scenario: 1
Temperature: 0.2
Max repair attempts: 3
```

Key Enzo baseline metrics:

```text
First-pass valid:       93.3%
Eventual valid:        100%
First-pass equivalent:  66.7%
Eventual equivalent:    96.7%
Avg output tokens:     138.3
Avg successful TTVED:  612.9
Repair rate:            33.3%
```

Complex Enzo baseline:

```text
Equivalent: 90%
TTVED:      893.9
Repair rate: 60%
```

Persisted Enzo first-pass semantic omissions included `async job`, `retry`, `result`, `approved`, and `booking confirmed`. The unresolved identity-provider scenario also used unsupported sequence blocks and later missed `session`.

## Prompt Change

Only Enzo sequence generation guidance changed.

Sequence user prompts now use compact sequence-specific requirement wording:

```text
- Represent these concepts:
- Mandatory participants:
- Required interactions:
- Use at least N interactions
```

The previous generic source-only line is replaced only for Enzo sequence prompts with:

```text
Silently check all listed requirements and Enzo sequence syntax. Return source only; no Markdown/explanations.
```

No checklist, reasoning, Markdown, commentary, or extra output is requested.

## Prompt Tokens

Measured with the benchmark tokenizer, `cl100k_base`, across the frozen 30 sequence scenarios. Counts include the unchanged Enzo system prompt plus the generated Enzo user prompt.

```text
Average before: 324.2
Average after:  330.2
Average delta:   +6.0
Min delta:        +5
Max delta:        +8
```

This stays within the `<= +10` token target.

## Invariants Preserved

Unchanged:

- Mermaid prompts and behavior
- Enzo grammar, parser, validator, renderer, and layout
- Enzo repair prompts and repair budget
- Semantic-equivalence rules
- Participant and interaction matching
- Benchmark scenarios and token accounting
- Mermaid rendering behavior
- Sequence source compactness goal

## Tests

Added prompt regression coverage for:

- Compact completeness reminder
- Mandatory required participants
- Required concept representation
- Single interaction minimum wording
- Required interaction constraints
- Source-only output instruction
- No flow/process cycle guidance in sequence user prompts
- Deterministic prompt-token impact against the legacy prompt

Relevant offline test command:

```powershell
dotnet test tests/Enzo.Diagrams.Benchmarks.Tests/Enzo.Diagrams.Benchmarks.Tests.csproj
```

## Target Metrics

Primary 30x1 targets:

```text
Complex Enzo repair rate:       <= 30%
Complex equivalent validity:    >= 95%
Overall first-pass equivalent:  >= 80%
Overall eventual equivalent:    >= 96.7%
MissingConcept repair attempts: <= 3
Enzo avg output tokens:         <= 140
Overall sequence TTVED:         < 550
Complex sequence TTVED:         < 750
```

Proceed to the 30x5 confirmation benchmark only if Enzo eventual equivalent is at least 95% and Enzo TTVED is at or near Mermaid TTVED with a clearly identified remaining optimization.

## Manual Benchmark Command

```powershell
$env:OPENAI_API_KEY = "your-openai-api-key"

dotnet run --project benchmarks/Enzo.Diagrams.LlmBenchmarks -- --suite sequence --runs 1
```

Do not run `--runs 5` until the 30x1 result meets the decision rule.
