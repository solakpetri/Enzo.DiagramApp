# LLM Benchmark Semantic Equivalence Baseline

## Executive Summary

This analysis applies the new deterministic semantic-equivalence checks to the persisted full benchmark run `llm-generation-cost-20260906-170959-706-gpt-4o-mini.json`. It uses offline re-evaluation only: no OpenAI calls, no regeneration, and no modification of historical source results.

The old syntax/render only benchmark was heavily confounded. Under the new checks only **1 of 30 Enzo generations and 1 of 30 Mermaid generations** qualify as Valid Equivalent Diagrams.

| Language | Syntax-valid | Render-valid | Kind-valid | Structurally complete | Semantically complete | Equivalent-valid |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| Enzo | 93.3% | 93.3% | 40.0% | 23.3% | 3.3% | 3.3% |
| Mermaid | 100.0% | 100.0% | 66.7% | 20.0% | 3.3% | 3.3% |

## How It Was Measured

The re-evaluation loads persisted attempts, reconstructs the scenario expectations from `benchmarks/scenarios`, re-extracts language-neutral diagram facts from each normalized generated source, and re-applies the deterministic checks. Output is written to `*-semantic.json`, `*-semantic.csv`, and `*-semantic.md` next to the original run. The original run files are unchanged.

## Equivalent By Category

| Category | Enzo equivalent | Mermaid equivalent |
| --- | ---: | ---: |
| flow | 1 / 10 | 1 / 10 |
| process | 0 / 10 | 0 / 10 |
| sequence | 0 / 10 | 0 / 10 |

The only scenario that passed for either language was `flow-login-basic`. Its successful-run tokens to valid equivalent diagram were 280 (Enzo) and 201 (Mermaid).

## Definition Failure: Requested Diagram Kind

Most generated diagrams were not the requested kind. Sequence-category scenarios were the worst confound: neither language produced a valid equivalent sequence diagram in this run.

| Language | Flow runs wrong kind | Sequence runs wrong kind | Process runs wrong kind |
| --- | ---: | ---: | ---: |
| Enzo | 9 / 10 | 9 / 10 | 0 / 10 |
| Mermaid | 1 / 10 | 8 / 10 | 1 / 10 |

Enzo generated `bpmn` for 18 of 20 flow/sequence scenarios; Mermaid generated `flowchart` for 8 of 10 sequence scenarios.

## Under-Specified Structure

Structural completeness failed more often than syntax: 23 of 30 Enzo runs and 24 of 30 Mermaid runs produced fewer nodes/edges/participants/interactions than the scenario minimum. This confirms cases such as `flow-warehouse-wave` (253 Enzo output tokens vs 110 Mermaid) were not close calls between complete diagrams but rather competing incomplete diagrams. Measured token gaps therefore understate the Mermaid completeness deficit.

## Semantically Complete

Only `flow-login-basic` (both languages) matched all required concepts. 29 of 30 runs in each language omitted at least one required concept under the deterministic concept matcher.

## Successful-Run Tokens to Valid Equivalent Diagram

TTVED is reported only for runs that reached an equivalent-valid attempt. With one passing run per language the sample is too small for meaningful comparison, which is precisely the point: the old TTV comparison compared mostly non-equivalent diagrams.

| Metric | Enzo | Mermaid |
| --- | ---: | ---: |
| Successful runs | 1 | 1 |
| Min / Max / Mean successful TTVED | 280 | 201 |

## Reproduction

```powershell
dotnet run --project benchmarks/Enzo.Diagrams.LlmBenchmarks -- `
  --reevaluate benchmarks/results/llm-generation-cost/llm-generation-cost-20260906-170959-706-gpt-4o-mini.json
```