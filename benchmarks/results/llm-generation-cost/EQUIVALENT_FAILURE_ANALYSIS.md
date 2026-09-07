# Equivalent Generation Failure Analysis

## 1. Executive summary

**Measured.** This analysis uses only the persisted benchmark run `llm-generation-cost-20260907-182108-236-gpt-4o-mini.json`, which matches the supplied 30-scenario baseline. No OpenAI calls were made and no benchmark inputs, prompts, validators, grammar, renderer, contracts, or result files were changed.

**Measured.** Initial output size is not the current primary disadvantage: Enzo averaged `171.6` output tokens and Mermaid `169.7`. Generation-only cost is also nearly tied: Enzo `333.5`, Mermaid `330.8`.

**Derived.** The successful-run TTVED gap is mostly not DSL source size. On successful equivalent runs, the `+138.1` token gap decomposes to `+48.4` initial input, `+8.6` initial output, `+51.8` semantic/structural repair input, and `+29.3` semantic/structural repair output.

**Measured.** All 6 unresolved Enzo runs are syntax/render invalid at final state. The failure modes are concentrated: unsupported cyclic flow/process structure in 4 runs, Mermaid-style `->|label|` edge labels in 2 flow runs, and unsupported sequence `alt/end` blocks in 1 sequence run. `flow-warehouse-wave` contains both Mermaid-style labels and later cycle regression.

**Derived.** Enzo’s most expensive TTV outlier, `flow-warehouse-wave`, reached `3042` tokens to the first syntax/render-valid diagram. Of that TTV, `76.5%` was syntax repair prompt replay/output, not initial generation.

**Recommendation.** The first optimization branch should target syntax repair/actionability and deterministic handling of the small set of LLM-hostile syntax patterns, not broad DSL compression.

## 2. Current baseline

**Measured.** Baseline source: `benchmarks/results/llm-generation-cost/llm-generation-cost-20260907-182108-236-gpt-4o-mini.json`.

| Metric | Enzo | Mermaid |
| --- | ---: | ---: |
| First-pass valid | 80.0% | 100.0% |
| Eventual valid | 83.3% | 100.0% |
| First-pass equivalent | 63.3% | 73.3% |
| Eventual equivalent | 80.0% | 83.3% |
| Syntax-valid | 80.0% | 100.0% |
| Avg output tokens | 171.6 | 169.7 |
| Avg TTV | 709.2 | 414.4 |
| Avg successful TTVED | 642.9 | 504.8 |

## 3. Unresolved Enzo inventory

**Measured.** Every unresolved Enzo run has `EquivalentValid = false`, `SyntaxValid = false`, and `RenderValid = false` at final state. Final total cost below means initial plus all repair tokens, while TTV is the benchmark `TokensToValidDiagram` value.

| Scenario | Category | Complexity | Initial tokens | Repairs | Stop | Final cost | Exact unresolved reason |
| --- | --- | --- | ---: | ---: | --- | ---: | --- |
| `flow-content-review` | flow | simple | 279 in / 96 out | 1 | identical-output | 789 | Valid-looking revision loop `requestRevisionsNode -> reviewNode` is rejected as a cycle. Repair repeated identical source. |
| `flow-data-import` | flow | complex | 297 / 233 | 1 | identical-output | 1334 | Mermaid edge-label leakage such as `schemaValidNode ->|yes| transformRecordsNode`; repair repeated identical source. |
| `flow-warehouse-wave` | flow | complex | 332 / 384 | 3 | none, budget exhausted | 4534 | Initial Mermaid label leakage, then valid but structurally short attempt, then semantic repair over-expanded and introduced cycles involving `loadTrailer`. |
| `process-contract-review` | process | medium | 295 / 177 | 3 | none, budget exhausted | 2206 | Redline loop alternated between `reviseContract -> legalReview` and `reviseContract -> legalDecision`, both rejected as cycles. |
| `process-manufacturing-quality` | process | complex | 304 / 208 | 2 | repair-oscillation | 1795 | Rework loop oscillated between `rework -> inspect` and `rework -> inspectionDecision`, both rejected as cycles. |
| `sequence-cache-lookup` | sequence | simple | 274 / 97 | 2 | identical-output | 1217 | Generated unsupported Mermaid-like sequence block `alt cacheMiss ... end`; repair failed to remove the block. |

**Measured source/failure inventory.** The persisted JSON contains complete source for each attempt. The failure-causing generated source lines and repair history are:

| Scenario | Contract | Attempt history |
| --- | --- | --- |
| `flow-content-review` | kind flow; concepts content submitted, review content, needs edits, request revisions, publish content; min 6 nodes, 6 edges, 1 decision | A0 `279/96`: `requestRevisionsNode -> reviewNode`; error cycle involving `reviewNode`. A1 Syntax repair `318/96`: identical source; same failures; stagnated. |
| `flow-data-import` | kind flow; concepts file uploaded, scan file, validate schema, transform records, duplicates, load records, notify owner; min 14 nodes, 17 edges, 4 decisions | A0 `297/233`: `schemaValidNode ->|yes| transformRecordsNode`, `duplicatesCheckNode ->|yes| loadRecordsNode`, `loadSuccessNode ->|no| endFailureNode : "Load Failed"`; parser reported unexpected `|` and trailing tokens. A1 Syntax repair `571/233`: identical source; same failures; stagnated. |
| `flow-warehouse-wave` | kind flow; concepts wave planned, reserve inventory, pick lists, assign pickers, pick zone, pack orders, quality scan, carrier, load trailer, dispatch trailer, tracking, exceptions, inventory, dashboard; min 42 nodes, 45 edges, 4 decisions | A0 `332/384`: many `->|yes|` and `->|label|` edges; syntax invalid. A1 Syntax repair `947/351`: removed dashboard `|label|` but kept decision `->|yes|`; syntax invalid. A2 Syntax repair `683/345`: syntax/render valid but only 19 nodes and 30 edges, missing tracking. A3 Structure+Semantic repair `575/917`: expanded to 40+ tasks but introduced cycles such as `confirmPickup -> loadTrailer`, `updateShippingStatus -> trackShipment`, and many `task -> updateDashboard` back-edges; final unresolved. |
| `process-contract-review` | kind process; concepts draft contract, legal review, redlines, revise contract, finance approval, signature, archive contract, reject contract; min 11 nodes, 12 edges, 2 decisions | A0 `295/177`: `reviseContract -> legalReview`; cycle involving `legalReview`. A1 Syntax repair `401/177`: changed to `reviseContract -> legalDecision`; cycle involving `legalDecision`. A2 `401/177`: reverted to `reviseContract -> legalReview`; same cycle. A3 `401/177`: changed back to `reviseContract -> legalDecision`; budget exhausted. |
| `process-manufacturing-quality` | kind process; concepts work order, assemble product, inspect product, rework, package product, batch record, release batch, hold, quality team; min 14 nodes, 16 edges, 3 decisions | A0 `304/208`: `rework -> inspect`; cycle involving `inspect`. A1 Syntax repair `432/209`: changed to `rework -> inspectionDecision`; cycle involving `inspectionDecision`. A2 `434/208`: reverted to `rework -> inspect`; A-B-A oscillation stopped. |
| `sequence-cache-lookup` | kind sequence; concepts client, api, cache, database, profile, cache miss; min 4 participants, 6 interactions | A0 `274/97`: generated `alt cacheMiss` and `end` block. A1 Syntax repair `326/97`: same unsupported block after blank-line normalization. A2 `326/97`: identical to A1; stagnated. |

## 4. Syntax failure analysis

**Measured.** Six initial Enzo generations were syntax/render invalid. All six remained unresolved or became unresolved again after repair.

| Failure class | Scenarios | What Enzo expected | Was it in prompt? | Error actionable? | Repair understood? | Likely prevention |
| --- | --- | --- | --- | --- | --- | --- |
| Unsupported cycle treated as syntax/render failure | `flow-content-review`, `process-contract-review`, `process-manufacturing-quality`; later `flow-warehouse-wave` | Acyclic flow/process graph accepted by current validator/layout | No; prompt shows syntax but does not say loops are illegal or how to model revision/rework | Partly; names one cycle node, not the edge/path to remove or an acyclic rewrite | Mostly no; identical output or oscillation | Validator/error improvement plus repair prompt guidance; possibly grammar/layout support for intentional loops later |
| Mermaid edge-label leakage | `flow-data-import`, `flow-warehouse-wave` | `From -> To : label` | Yes, clearly shown | Yes for humans, but noisy repeated `Unexpected character '|'` hid the rewrite | Partial; one scenario identical, warehouse needed two syntax repairs | Deterministic normalization or parser tolerance for `A ->|x| B`; prompt reminder also possible |
| Unsupported sequence combined fragment | `sequence-cache-lookup` | Only actor/participant declarations and `From -> To: message` / `From --> To: return message` | The allowed syntax is shown, but unsupported `alt/end` is not explicitly forbidden | Partly; parser reports missing arrow/colon and unexpected `end`, not “alt blocks unsupported” | No; repeated block | Validator/error improvement and repair prompt; optionally grammar support or deterministic lowering |

**Derived.** Syntax reliability is Enzo-specific in this run. Mermaid had 100% CLI syntax/render validity. However, two Mermaid eventual-equivalence failures came from the benchmark semantic extractor not supporting valid Mermaid inline node declarations on labeled edges; that is separate from Mermaid CLI syntax validity.

## 5. 3,042-token outlier

**Measured.** `flow-warehouse-wave` is the Enzo max TTV outlier.

| Stage | Repair type | Input | Output | Cumulative to stage | Validation/equivalence result |
| --- | --- | ---: | ---: | ---: | --- |
| A0 generation | none | 332 | 384 | 716 | Syntax/render invalid due Mermaid-style `->|label|`; kind/structure/semantic unknown. |
| A1 repair | Syntax | 947 | 351 | 2014 | Still syntax invalid; decision labels still `->|yes|`. |
| A2 repair | Syntax | 683 | 345 | 3042 | First syntax/render-valid diagram; kind valid, but structurally short: 19 nodes vs 42, 30 edges vs 45, missing tracking. |
| A3 repair | Structure+Semantic | 575 | 917 | 4534 | Final source over-expanded and introduced cycles involving `loadTrailer`; syntax/render invalid again; equivalent unresolved. |

**Derived TTV decomposition.** The `3042` TTV includes only A0 through A2 because A2 is the first valid diagram.

| Component | Tokens | Percent |
| --- | ---: | ---: |
| Initial instruction/scenario input | 332 | 10.9% |
| Initial output | 384 | 12.6% |
| Syntax repair input | 1630 | 53.6% |
| Syntax repair output | 696 | 22.9% |
| Semantic repair input | 0 | 0.0% |
| Semantic repair output | 0 | 0.0% |
| Other | 0 | 0.0% |

**Derived.** The run is `3042 / 709.2 = 4.3x` the Enzo mean TTV because a complex initial output replayed through two syntax repairs before any valid diagram existed. The final unresolved state is a combination of repair-loop problem, validation-error actionability, model stagnation around Mermaid labels, and structural complexity. It is not primarily a source-size problem.

## 6. Stagnation analysis

**Measured.** Four Enzo runs stopped due to stagnation.

| Scenario | Type | Wrong initially | Feedback provided | Model changed | Failed to change | Tokens spent | Estimated tokens prevented |
| --- | --- | --- | --- | --- | --- | ---: | ---: |
| `flow-content-review` | identical-output | Cycle from revision back to review | “Syntax/parser validation failed: cycle involving reviewNode” plus complete current source | Nothing | Did not remove or unfold loop | 789 | ~828 |
| `flow-data-import` | identical-output | `->|label|` Mermaid labels | Repeated unexpected `|`/trailing-token errors | Nothing | Did not rewrite to `-> target : label` | 1334 | ~1608 |
| `process-manufacturing-quality` | oscillation | Rework loop | Cycle involving `inspect`, then `inspectionDecision` | Toggled target | Did not remove/unroll loop | 1795 | ~642 |
| `sequence-cache-lookup` | identical-output | Unsupported `alt/end` block | Missing arrow/colon and unexpected `end` | Removed one blank line only | Did not remove `alt/end` | 1217 | ~423 |

**Hypothesis.** One repair-system improvement could eliminate several failures if it gives explicit rewrite recipes for cycles, Mermaid label leakage, and unsupported sequence blocks instead of replaying generic syntax errors.

## 7. TTVED gap decomposition

**Measured/Derived.** Successful equivalent runs only: Enzo `24`, Mermaid `25`.

| Component | Enzo avg | Mermaid avg | Gap |
| --- | ---: | ---: | ---: |
| Initial input | 289.2 | 240.8 | +48.4 |
| Initial output | 164.7 | 156.1 | +8.6 |
| Syntax repair input | 0.0 | 0.0 | 0.0 |
| Syntax repair output | 0.0 | 0.0 | 0.0 |
| Semantic/structural repair input | 115.2 | 63.4 | +51.8 |
| Semantic/structural repair output | 73.9 | 44.6 | +29.3 |
| Other | 0.0 | 0.0 | 0.0 |
| Total successful TTVED | 642.9 | 504.8 | +138.1 |

**Derived.** Of the `~92` tokens beyond the known initial-input tax, `~81.1` comes from higher repair input/output on successful Enzo runs and `~8.6` from larger successful-run initial output.

## 8. Instruction-overhead analysis

**Measured.** Enzo initial input averaged `290.7`; Mermaid `244.7`; Enzo tax `+46.0`. The Enzo system prompt is 640 characters / 106 whitespace words / 32 lines. Mermaid is 499 characters / 66 words / 22 lines.

| Enzo section | Classification | Notes |
| --- | --- | --- |
| Output-only and no fences | Necessary | Shared constraint; not Enzo-specific. |
| Use requested diagram kind | Necessary | Shared semantic guard. |
| Flow syntax block | Necessary but compressible | Proprietary DSL; needed for `gpt-4o-mini`, but examples can likely be denser. |
| Sequence syntax block | Necessary but compressible | Needed; should explicitly say no `alt/end` if not supported. |
| BPMN syntax block | Necessary but compressible | Needed; current examples do not mention acyclic validator constraint. |
| Identifier rule | Necessary | Prevents invalid IDs and undefined references. |
| Declaration-before-reference rule | Necessary | Prevents parser/validator failures. |
| Repeated labels/examples | Potentially compressible | Same `From -> To` pattern repeated for flow and bpmn. |
| Semantic instructions | Mostly not redundant | Semantic contract is in user prompt; system prompt mostly syntax. |

**Estimated.** Current Enzo overhead: `+46`. Realistically removable without hurting reliability: `15-25` tokens. Likely unavoidable proprietary-DSL tax on `gpt-4o-mini`: `20-30` tokens unless Enzo becomes known to the model or syntax becomes Mermaid-compatible in common constructs.

## 9. First-pass equivalent failures

**Measured.** Enzo first-pass equivalent failures: 11 of 30.

| Failed layer | Count | Scenarios | Enzo-specific? |
| --- | ---: | --- | --- |
| Syntax/render/kind/structure/semantic cascade | 6 | unresolved syntax set above | Yes, primarily Enzo syntax/validator/prompt mismatch. |
| Structure only | 4 | `flow-invoice-approval`, `flow-loan-application`, `flow-release-pipeline`, `process-subscription-lifecycle` | Mostly general task-completion pressure from minimum counts; Enzo repairs succeeded but were expensive. |
| Semantic only | 1 | `sequence-claims-processing` missing `payout` | General LLM omission; repair succeeded. |

## 10. Complex-diagram observation

**Measured observation.** Complex eventual equivalent was Enzo `9/12` and Mermaid `7/12` in this one-run sample. Do not treat this as statistically significant.

| Scenario where Enzo passed and Mermaid failed | Mermaid failure reason | Observation |
| --- | --- | --- |
| `flow-loan-application` | Semantic extractor could not handle valid-looking Mermaid inline node form `Risk -->|Pass| Manual`; unresolved after identical repair. | Enzo avoided Mermaid inline declaration syntax and eventually met structure after expensive repair. |
| `process-incident-management` | Semantic extractor could not handle `Severity -->|High| Responders[Notify Responders]`; identical repair. | Enzo’s explicit declarations made extraction straightforward in this run. |
| `process-subscription-lifecycle` | Structural miss: expected 4 decisions, found 2. | Enzo repaired decision count; Mermaid stagnated. |

**Hypothesis requiring repeated runs.** Enzo’s explicit declaration style may preserve complex requirements better than Mermaid when models use compact inline Mermaid constructs that downstream semantic tooling does not extract. This needs repeated runs and possibly a Mermaid extractor audit before becoming a product claim.

## 11. Sequence analysis

**Measured.** Sequence is Enzo’s strongest category: Enzo `9/10`, Mermaid `10/10`; Enzo successful TTVED `499.4`, Mermaid `408.8`.

| Metric | Enzo | Mermaid | Gap |
| --- | ---: | ---: | ---: |
| Avg initial input | 284.6 | 236.6 | +48.0 |
| Avg output | 147.3 | 172.2 | -24.9 |
| Repair runs | 2 | 0 | +2 |
| Syntax first-pass failures | 1 | 0 | +1 |
| Avg successful TTVED | 499.4 | 408.8 | +90.6 |

**Derived.** Sequence already has lower Enzo output tokens than Mermaid by `24.9` tokens on average in this run. The category can plausibly become first to reach `Enzo TTVED < Mermaid` if the `sequence-cache-lookup` syntax failure is eliminated and prompt overhead is reduced. Estimated: removing the one sequence syntax failure improves validity to `10/10`; reducing prompt overhead from +48 to +20 would put most successful first-pass sequence runs within a few tokens of Mermaid, and Enzo’s lower output size may overcome the remaining tax.

## 12. Flow analysis

**Measured.** Flow is the weakest Enzo category: Enzo `7/10`, Mermaid `7/10`; Enzo successful TTVED `940.7`, Mermaid `675.0`.

| Driver | Evidence |
| --- | --- |
| Decisions and branch labels | `flow-data-import` and `flow-warehouse-wave` used Mermaid `->|label|`, causing syntax repairs/stagnation. |
| Loops/cycles | `flow-content-review` revision loop rejected; `flow-warehouse-wave` final semantic repair introduced cycles. |
| Complex structure minimums | `flow-loan-application`, `flow-release-pipeline`, and `flow-warehouse-wave` needed many nodes/edges/decisions. |
| Repair loops | Flow repair token totals: `831.2` average over all flow runs; successful flow repairs include `529`, `1846`, and `901` token costs. |

**Derived.** Flow cost is primarily repair behavior around decisions/branch labels and complex structural expansion, not base Enzo source size.

## 13. Process analysis

**Measured.** Process sits closer to Mermaid: Enzo `8/10`, Mermaid `8/10`; Enzo successful TTVED `543.6`, Mermaid `476.0`.

| Comparison | Finding |
| --- | --- |
| Syntax reliability | Enzo had 2 syntax failures; both were cycles from natural rework/redline loops. |
| Repair frequency | Enzo 3 repair runs, Mermaid 3 repair runs. |
| Output size | Enzo avg output `168.7`, Mermaid `160.4`; small gap. |
| Structural completeness | Enzo and Mermaid both had 3 first-pass structural failures. |
| Design to preserve | Explicit `bpmn` declaration and separate node declarations worked well when diagrams were acyclic. |

**Hypothesis.** Process is closer because BPMN outputs are less likely than flow to use Mermaid-style edge labels; the remaining Enzo-specific issue is cycle handling for realistic process loops.

## 14. Counterfactual cost estimates

**Estimated.** These are diagnostic calculations from persisted data, not benchmark results.

| Counterfactual | Estimate | Interpretation |
| --- | ---: | --- |
| A: no syntax repair cost in TTV | Enzo avg TTV `~462` instead of `709` | Removing syntax repair loops would make valid-diagram cost competitive, but not guarantee equivalent validity. |
| Add unresolved runs as first-pass equivalent at their initial token cost | Enzo successful avg `~613.5` over 30 | Syntax reliability alone does not beat Mermaid `504.8`. |
| Current successful Enzo with all successful repair tokens removed | `~453.8` | First-pass equivalent reliability would beat current Mermaid and approach stretch. |
| Cap successful Enzo repair outlier `flow-loan-application` near median successful repair cost | `~595.7` | One successful repair outlier costs about `47` avg TTVED tokens. |
| Prompt compression from +46 to +20 | `~616.9` current successful mix | Helpful but insufficient alone. |
| Prompt compression from +46 to +0 | `~596.9` current successful mix | Still insufficient alone. |
| Combined realistic: remove successful structural repair outliers, eliminate syntax stagnation, compress prompt by 20-25 | `~500-540` initially; lower if first-pass structure improves | Plausibly reaches Mermaid, but stretch `~404` requires major first-pass-equivalence gains plus prompt compression. |

## 15. Ranked optimization opportunities

| Priority | Problem | Evidence | Affected scenarios | Current token cost | Estimated recoverable tokens | Area | Risk | Recommended experiment |
| ---: | --- | --- | --- | ---: | ---: | --- | --- | --- |
| 1 | Syntax repair is not actionable for common Enzo-invalid patterns | 6/6 unresolved Enzo runs syntax/render invalid; 4 stagnation stops | all unresolved set | `7407` TTV tokens above initial on syntax-failure runs | High: `150-250` avg TTV, plus validity | Repair prompt, validator errors, deterministic normalization | Low-medium | Offline classify errors and add targeted repair guidance tests before rerun. |
| 2 | Mermaid-style edge labels are not normalized | `flow-data-import` stagnated; `flow-warehouse-wave` spent two syntax repairs | flow decisions | `804` and `2326` TTV repair tokens before valid diagram | Medium-high | Parser tolerance or deterministic normalization | Low if syntax-preserving | Accept/lower `A ->|x| B` to `A -> B : x` in a narrow normalization experiment. |
| 3 | Cycle errors lack rewrite path | 4 unresolved runs include natural revision/rework cycles | flow/process loops | `414`, `1734`, `1283`, plus warehouse final regression | Medium-high | Validator errors, repair prompt, maybe layout/DSL later | Medium | Add cycle path/error tests and repair recipe: unfold loop to explicit review/rework continuation. |
| 4 | Successful structural repairs are expensive | Enzo successful repair avg adds `189.1` tokens/run; flow-loan repair total `1846` | 5 successful repaired runs | `4538` successful repair tokens total | Medium | Generation prompt and repair algorithm | Medium | Improve minimum-count instruction and repair prompt to add only missing counts/concepts. |
| 5 | Enzo proprietary prompt tax | `+46` initial input | all Enzo runs | `~46` per run | Medium: `15-25` per run realistic | Generation prompt | Medium if over-compressed | Compress repeated flow/bpmn syntax examples after reliability fixes. |
| 6 | Unsupported sequence `alt/end` blocks | 1 simple sequence unresolved | `sequence-cache-lookup` | `846` syntax repair tokens | Category-specific medium | Sequence syntax guidance or grammar | Low-medium | Add explicit “no alt/end blocks” repair guidance or support lowering. |

## 16. Recommended first optimization branch

Recommended first optimization branch:
`optimize/enzo-syntax-repair-actionability`

Objective:
Make Enzo generation/repair reliably recover from the three observed syntax-class patterns without changing semantic contracts or benchmark definitions: Mermaid-style `->|label|` edge labels, unsupported sequence `alt/end` blocks, and cycle errors that need explicit acyclic rewrites.

Why first:
All 6 unresolved Enzo runs are syntax/render failures, and 4 stagnation stops show generic repair feedback is not actionable enough. Prompt compression alone can recover only an estimated `15-25` tokens per successful run, while syntax repair failures consume hundreds to thousands of tokens and suppress eventual equivalent validity.

Expected impact:
Raise Enzo eventual equivalent validity from `80%` toward `90-95%` in the immediate next benchmark, reduce max TTV by eliminating the `3042` outlier pattern, and reduce average TTV substantially before any DSL-size optimization.

Benchmark metric expected to improve:
Syntax-valid, render-valid, eventual equivalent-valid, stagnation stops, avg TTV, max TTV, and eventually successful-run TTVED once repaired syntax failures become equivalent-valid instead of unresolved.

What must NOT change:
Frozen scenarios, semantic contracts, equivalence thresholds, Mermaid prompts, Enzo prompts unless explicitly scoped inside the optimization branch, repair accounting, success definitions, and persisted historical results.

## 17. Targets for the next benchmark

**Estimated immediate target after the first optimization.** Enzo syntax-valid/render-valid `>= 96.7%`, stagnation stops `<= 1`, eventual equivalent `>= 90%`, max TTV `< 1500`, and average TTV `< 550`.

**Long-term target remains unchanged.** Enzo eventual equivalent validity `>= 99%`; Enzo first-pass equivalent validity within 1-2 percentage points of Mermaid; average TTVED Enzo `<` Mermaid; stretch Enzo `>= 20%` cheaper than Mermaid.
