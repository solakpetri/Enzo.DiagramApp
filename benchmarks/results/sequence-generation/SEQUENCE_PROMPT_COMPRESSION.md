# Sequence Prompt Compression

## Hypothesis

Enzo sequence output is more compact than Mermaid output, but the proprietary DSL instruction prompt erases much of that advantage. This branch keeps the DSL, Mermaid prompt, repair behavior, benchmark scenarios, and semantic benchmark unchanged while reducing the Enzo sequence initial-generation prompt.

## Current Baseline

Latest dedicated sequence benchmark: `sequence-generation-20260909-190440-813-gpt-4o-mini`.

```text
Model: gpt-4o-mini
Scenarios: 30
Runs per scenario: 1
Temperature: 0.2
Max repair attempts: 3
```

```text
First-pass valid:       Enzo 96.7%, Mermaid 100%
Eventual valid:         Enzo 100%,  Mermaid 100%
First-pass equivalent:  Enzo 76.7%, Mermaid 86.7%
Eventual equivalent:    Enzo 96.7%, Mermaid 100%
Avg output tokens:      Enzo 140.2, Mermaid 156.7
Avg successful TTVED:   Enzo 568.9, Mermaid 533.8
Repair rate:            Enzo 23.3%, Mermaid 13.3%
Cold-start input:       Enzo 338.9, Mermaid 284.9 (+54.0)
```

## Old Prompt Structure

Measured with the benchmark tokenizer, `cl100k_base`. Fixed sections are from the pre-compression Enzo system prompt; user sections are from the generated Enzo sequence user prompt.

| Section | Tokens | Required? | Action |
| --- | ---: | --- | --- |
| System behavior/output-only | 28 | yes | keep/compress |
| Flow syntax | 35 | no for sequence | remove from sequence generation |
| Sequence syntax | 30 | yes | keep as compact canonical example |
| Business process syntax | 33 | no for sequence | remove from sequence generation |
| Identifier rule | 21 | yes | keep shorter sequence-scoped rule |
| Completeness/source-only user line | 22 | yes | compress |
| Enzo sequence/not flow/BPMN user line | 18 | partly | remove; sequence-only system plus diagram-kind requirement covers it |

## Removed Or Replaced Wording

The sequence initial-generation system prompt no longer includes flowchart or BPMN/process syntax. The separate user line `Use Enzo sequence syntax, not flow or bpmn` was removed for sequence generation only because the system prompt is now sequence-only and the semantic contract still sends `Diagram kind: sequence`.

The completeness reminder was preserved but compressed to:

```text
Before output, silently verify required participants, concepts, interactions, and counts; return source only.
```

## New Prompt Structure

```text
Compact sequence-only Enzo syntax
+
Original scenario text
+
Shared semantic requirements
+
Compact completeness/source-only reminder
```

The new fixed Enzo sequence syntax prompt teaches only:

```text
sequence Name
actor U "User"
participant Api "API"
U -> Api: Request
Api --> U: Response
```

plus the short identifier/declaration rule.

## Token Reduction

Counts below are local deterministic `cl100k_base` counts for system prompt plus generated user prompt across the frozen 30 sequence scenarios. OpenAI chat-envelope overhead is not included in these local counts, but the before count tracks the previous API baseline closely.

```text
Before Enzo sequence system prompt:       152
After Enzo sequence system prompt:         48

Before avg Enzo system+user prompt:       332.2
After avg Enzo system+user prompt:        207.2
Tokens removed:                           125.0
Percentage reduction:                      37.6%

Mermaid avg system+user prompt:           276.2
Remaining raw Enzo-vs-Mermaid difference: -69.0
Expected Enzo API input from baseline:    ~214 tokens
Expected API difference vs Mermaid:       ~71 fewer input tokens
```

## Information Preserved

The model still receives the original request, expected diagram kind, required concepts, mandatory participants, required interactions, minimum participant count, minimum interaction count, source-only instruction, and silent completeness reminder.

## Benchmark Invariants Preserved

Unchanged: Enzo grammar, parser, validator, renderer, repair prompt, repair algorithm, repair budget, semantic contracts, semantic-equivalence validation, scenarios, participant/interaction matching, Mermaid prompt and behavior, benchmark token accounting, and OpenAI model/settings.

## Tests

Added or updated offline prompt regression coverage for sequence-only syntax, no flow/BPMN guidance in Enzo sequence initial prompts, preserved completeness reminder, preserved semantic contract fields, source-only behavior, sequence-specific system prompt selection, and fixed prompt-size bounds.

```powershell
dotnet test tests/Enzo.Diagrams.Benchmarks.Tests/Enzo.Diagrams.Benchmarks.Tests.csproj
```

## Manual Benchmark Command

Do not run live benchmarks without an API key. The next manual check is the 30x1 sequence run:

```powershell
$env:OPENAI_API_KEY = "your-openai-api-key"

dotnet run --project benchmarks/Enzo.Diagrams.LlmBenchmarks -- --suite sequence --runs 1
```

Proceed to `--runs 5` only if Enzo eventual equivalent is at least 95% and preferably Enzo successful TTVED is at or below Mermaid.
