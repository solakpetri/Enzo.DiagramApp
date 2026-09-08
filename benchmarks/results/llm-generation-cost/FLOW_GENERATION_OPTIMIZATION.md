# Enzo Flow Generation Optimization

## 1. Baseline

Stationary control baseline supplied for `gpt-4o-mini`, 30 scenarios, one run per scenario, temperature `0.2`, and at most three repair attempts:

| Metric | Enzo baseline |
| --- | ---: |
| First-pass valid | 70% |
| Eventual valid | 90% |
| First-pass equivalent-valid | 56.7% |
| Eventual equivalent-valid | 86.7% |
| Syntax-valid | 86.7% |
| Render-valid | 86.7% |
| Avg output tokens | 171.1 |
| Avg repair tokens | 601.9 |
| Avg TTV | 764.9 |
| Median TTV | 495 |
| Avg successful TTVED | 789.9 |
| Median successful TTVED | 495 |

Flow was the largest cost problem: 7 / 10 equivalent-valid with successful TTVED 1261.6 versus Mermaid 667.3. Process was 9 / 10 equivalent-valid with successful TTVED 736.4. Sequence was already strong at 10 / 10 equivalent-valid and must remain protected.

## 2. Observed Flow/Process Failures

Evidence came from `FAILURE_ANALYSIS.md` and `REPAIR_CONVERGENCE_OPTIMIZATION.md`. The requested `EQUIVALENT_FAILURE_ANALYSIS.md` was not present in this checkout.

| Failure | Evidence | Generation rule added |
| --- | --- | --- |
| Cycle | 10 repair attempts in the latest baseline; concrete rework/revision loops in `flow-content-review`, `process-manufacturing-quality`, and `process-contract-review`. | Flow/BPMN prompts now require acyclic graphs, no back-edges, and forward retry/rework unfolding. |
| InvalidLabel | 6 repair attempts; concrete failures from unquoted multiword labels in password reset/healthcare referral and Mermaid pipe labels in data import/warehouse wave. | Flow/BPMN prompts now show `A -> B : yes`, prohibit `A ->|yes| B`, and prefer quoted or short labels. |
| MissingStructure | 6 repair attempts; semantic diagnostics already provide minimum nodes, edges, decisions, participants, and interactions. | Flow/BPMN prompts now compactly restate minimum counts and require a silent counts/concepts check. |
| UndefinedReference / InvalidIdentifier | Observed `From` undefined reference and reserved-looking `decision` identifier. | Flow/BPMN prompts now require short safe identifiers and declaration before reference. |

## 3. Prompt Changes

Changes are restricted to Enzo generation prompts and diagnostics:

- Enzo flow/process user prompts use a compact Enzo-only format for request, concepts, and structural minimums.
- Flow uses Enzo `flow`, not sequence/BPMN.
- Process uses Enzo `bpmn`, not flow/sequence.
- Sequence keeps the existing user-prompt guidance and does not receive flow/BPMN cycle rules.
- The shared Enzo system prompt, Mermaid prompt, repair prompts, grammar, parser, validator, renderer, semantic contracts, scenarios, thresholds, scoring, and token accounting were not changed.

## 4. Cycle Strategy

Generation now teaches the forward-only representation for natural loops:

```text
Rework -> Reinspect
```

instead of connecting a later rework node back to the original inspection/review node. This preserves retry/rework semantics without violating Enzo flow/BPMN acyclicity.

## 5. Label Strategy

Generation now uses the real Enzo edge-label form:

```text
A -> B : yes
```

and explicitly prohibits Mermaid pipe labels:

```text
A ->|yes| B
```

It also tells the model to quote multiword labels or prefer short branch labels such as `yes`, `no`, `approved`, and `rejected` when semantically equivalent.

## 6. Structural-Completeness Strategy

The benchmark user prompt already carries the semantic contract. Flow/process Enzo prompts now compactly preserve those minimums, for example:

```text
Need: flow; concepts approval, rejection, rework; >=8 nodes, >=9 edges, >=2 decisions.
```

The prompt also instructs a silent pre-output check for kind, references, labels, counts, and concepts. The checklist is not requested in model output.

## 7. Prompt-Token Impact

Offline estimate uses `cl100k_base` over the Enzo system prompt plus a representative flow/process user prompt with three concepts and node/edge/decision minimums. No OpenAI call was made.

| Prompt | Previous Enzo generation input | New Enzo generation input | Difference |
| --- | ---: | ---: | ---: |
| Flow | 251 | 270 | +19 |
| Process | 251 | 271 | +20 |
| Sequence | 251 | 251 | 0 |

The flow/process increase stays within the `<= +20` target for this representative prompt, and sequence is unchanged.

## 8. Benchmark Invariants Preserved

This branch does not change the 30 scenarios, complexity labels, expected kinds, semantic concepts, structural minimums, TTV/TTVED calculation, comparison threshold, repair budget, OpenAI model, temperature, Mermaid prompt or behavior, Enzo grammar/parser/validators/renderers, repair algorithm, benchmark success criteria, or historical result files.

## 9. Tests

Added deterministic tests with no live OpenAI calls for:

- Flow cycle guidance, no back-edges, and forward retry/rework strategy.
- BPMN/process cycle guidance.
- Enzo edge-label syntax and Mermaid pipe-label prevention.
- Structural minimum counts in the prompt.
- Short safe identifier and declaration guidance.
- Sequence prompt regression: no flow/BPMN cycle-specific guidance.
- Source-only/no-Markdown output contract through the unchanged Enzo system prompt.
- Markdown reporting of first-pass Enzo failure categories.

## 10. Expected Benchmark Impact

Expected impact is fewer preventable first-pass flow/process failures entering repair, especially cycle, invalid-label, missing-structure, undefined-reference, and invalid-identifier cases. Targets remain the supplied targets: cycle repair attempts `10 -> <= 2`, invalid-label repair attempts `6 -> <= 2`, missing-structure repair attempts `6 -> <= 3`, flow equivalent validity at least 90%, process equivalent validity at least 90%, and sequence eventual equivalent at least 90% with 10 / 10 preferred.

These are targets, not measured results. No live benchmark was automatically run.

## 11. Manual Benchmark Command

Run the stationary 30-scenario, one-run benchmark manually:

```powershell
$env:OPENAI_API_KEY = "your-openai-api-key"

dotnet run --project benchmarks/Enzo.Diagrams.LlmBenchmarks -- --runs 1
```
