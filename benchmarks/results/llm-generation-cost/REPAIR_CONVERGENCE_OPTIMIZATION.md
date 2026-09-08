# Enzo Repair Convergence Optimization

## 1. Baseline repair problem

The stationary control baseline is the supplied `gpt-4o-mini` run with 30 scenarios, one run per scenario, temperature `0.2`, and at most three repair attempts.

| Metric | Enzo baseline |
| --- | ---: |
| Eventual equivalent-valid | 86.7% |
| Syntax-valid | 86.7% |
| Equivalent unresolved | 4 |
| Stagnation stops | 3 |
| Avg repair tokens | 495.8 |
| Median successful TTVED | 475.5 |
| Avg successful TTVED | 865.3 |
| Max TTV | 2206 |
| Max TTVED | 2983 |

The pre-branch failure analysis report identified repair actionability and convergence as the highest-value Enzo optimization. Inspection of the matching latest persisted baseline confirmed four unresolved Enzo shapes:

- `flow-content-review`: an identical repair retained an unsupported cycle.
- `flow-warehouse-wave`: an identical repair retained Mermaid-style `A ->|label| B` edges.
- `process-contract-review`: repairs alternated between two cyclic back-edges until the budget was exhausted.
- `process-healthcare-referral`: an identical repair retained unquoted multiword edge labels.

The same baseline also contained two valid structural repairs that introduced a new syntax failure: a cycle in `flow-data-import` and undefined BPMN references in `process-incident-management`.

## 2. Failure categories addressed

The benchmark now assigns one small Enzo-specific category to each repair request:

```text
Syntax
UndefinedReference
InvalidIdentifier
InvalidLabel
InvalidEdge
WrongDiagramKind
MissingStructure
MissingConcept
Cycle
Other
```

Classification reuses parser, validator, and semantic diagnostics. Source inspection is limited to recognizing actionable syntax shapes such as Mermaid edge labels, unsupported sequence blocks, reserved identifiers, missing declaration kinds, and unquoted multiword edge labels. It does not normalize or accept invalid source.

## 3. Repair-prompt changes

Enzo repair requests now contain only the current source, precise failures, applicable syntax guidance, preservation instructions, and the complete-source output requirement.

Targeted guidance covers:

- `A ->|label| B` rewritten as `A -> B : label`.
- Multiword edge labels quoted as `A -> B : "multiword label"`.
- Undefined references corrected to an existing declaration or declared only when required.
- Reserved identifiers renamed with all references updated.
- Flow/BPMN cycles removed or unfolded into forward-only structure.
- Unsupported sequence `alt`, `else`, and `end` blocks replaced by direct message lines.
- Missing concepts and structural counts reported using the existing semantic diagnostics.

Every Enzo repair says to preserve valid declarations, structure, labels, and semantics and to change only what the listed failures require. Semantic repairs add only missing concepts or structure. They also keep added flow/process edges acyclic and require new nodes or participants to be declared before reference.

Repair calls use a dedicated 26-word Enzo repair system prompt rather than replaying the 106-word generation tutorial. Mermaid continues to use its existing system prompt and repair builder unchanged. Actual token savings remain an API-measured benchmark result; the offline tests only verify prompt content and relative size for known failure shapes.

## 4. Stagnation changes

The existing source-equality protection remains active.

- A first identical Enzo repair receives one stronger prompt stating that the source did not change and must be modified.
- A repeated identical result stops with `identical-output`.
- An Enzo `A -> B -> A` oscillation receives one final invariant-focused repair when budget remains.
- A failed final oscillation repair stops with `repair-oscillation`.
- Mermaid retains its previous immediate stagnation behavior.
- The maximum repair budget remains three.

## 5. Token-accounting guarantees

All model calls continue to use OpenAI response usage as the authoritative token source. Every completed repair request and response is appended exactly once and remains included in repair input, output, total-token, TTV, and TTVED calculations.

The optimization does not change:

- `TokensToValidDiagram`.
- `TokensToValidEquivalentDiagram`.
- successful-only TTVED aggregation.
- unresolved-run behavior.
- API usage accounting.

Each persisted Enzo repair attempt now also records its repair type, failure category, success status, and whether it introduced a syntax failure. Input and output tokens were already recorded per attempt and remain unchanged.

## 6. Benchmark invariants preserved

This branch does not change Mermaid prompts, repair behavior, validation, or extraction. It also does not change scenarios, scenario metadata, semantic contracts, expected kinds, structural thresholds, required concepts, equivalence rules, the 95% comparison threshold, token formulas, the Enzo grammar/parser/validators, rendering, layout, or production API behavior.

Historical result files are not modified.

## 7. Diagnostics and tests

Markdown reports now include average repair attempts, average repair input/output tokens, repair success rate, category counts, and these Enzo convergence metrics:

- Repair attempts started.
- Repairs that resolved syntax.
- Repairs that resolved semantic failure.
- Repairs that introduced a new syntax failure.
- Identical-output repairs.
- Oscillation repairs.
- Runs unresolved after exhausting the repair budget.

CSV output includes repair failure categories, per-repair success values, and introduced-syntax-failure counts. JSON retains full per-attempt diagnostics.

Deterministic tests cover Mermaid syntax leakage, undefined references, reserved identifiers, invalid branch labels, unsupported sequence blocks, cycles, missing concepts, structural shortfalls, one identical-output escalation, repeated identical stopping, one final oscillation repair, Mermaid stagnation preservation, syntax regression diagnostics, report fields, and exact-once token accounting. Tests use queued fake model responses and make no OpenAI calls.

## 8. Expected benchmark impact

The expected immediate effect is fewer repeated syntax repairs, fewer repair-induced syntax regressions, and materially smaller repair inputs. The branch targets eventual equivalent validity of at least 93%, syntax validity of at least 95%, no more than two unresolved runs, no more than one stagnation stop, median successful TTVED below 450, and max Enzo TTV below 1200 without changing the control.

These are targets, not measured results. No live benchmark was run automatically.

## 9. Manual benchmark command

Run the stationary 30-scenario, one-run benchmark after the branch is merge-ready:

```powershell
$env:OPENAI_API_KEY = "your-openai-api-key"

dotnet run --project benchmarks/Enzo.Diagrams.LlmBenchmarks -- --runs 1
```
