# Enzo LLM Generation Cost Failure Analysis
## Executive Summary
This analysis uses the persisted full benchmark run `llm-generation-cost-20260906-170959-706-gpt-4o-mini.json`. No OpenAI calls were rerun and no persisted results were modified.

Measured facts:
| Finding | Evidence |
| --- | --- |
| Enzo's token disadvantage is primarily repair cost. | Enzo is +247.4 average tokens to valid diagram versus Mermaid; +166.3 tokens, or 67.2% of the gap, comes from repairs. |
| Six Enzo runs failed first pass; two failed after all repairs. | First-pass failures: `flow-content-review`, `flow-password-reset`, `flow-data-import`, `flow-loan-application`, `flow-warehouse-wave`, `process-manufacturing-quality`. Final failures: `flow-content-review`, `flow-password-reset`. |
| Flow is the main reliability problem. | Enzo flow first-pass validity was 50%; process was 90%; sequence-category scenarios were 100%. |
| The 1,830-token outlier was a repair-loop failure. | `flow-password-reset` returned the same invalid source four times. Three repair calls cost 1,506 tokens. |
| Generated Mermaid is much smaller than static Mermaid fixtures. | Static average Mermaid fixture size was 147 tokens; generated Mermaid output averaged 79.3 tokens. Generated Enzo averaged 116.4 versus 148.8 static fixture tokens. |
| Benchmark validity does not enforce requested diagram kind. | Enzo generated `bpmn` for 26 of 30 initial runs, including 9 of 10 flow scenarios and 8 of 10 sequence scenarios. Mermaid generated `flowchart` for 28 of 30 runs, including 8 of 10 sequence scenarios. |

Likely explanation: Enzo is not currently expensive because its persisted fixture representation is intrinsically much larger. It is expensive because the model more often creates invalid Enzo BPMN/flow-style source, the repair loop resends full invalid source plus validation errors, and generated Mermaid often uses shorter, sometimes less complete, valid flowchart shortcuts.

Hypothesis requiring another experiment: enforcing diagram-kind alignment and semantic equivalence would likely increase Mermaid output cost and may change the apparent sequence advantage, because the current LLM benchmark mostly validates syntax/rendering rather than whether a sequence request produced a sequence diagram.

## Baseline Metrics
| Metric | Enzo | Mermaid | Delta |
| --- | ---: | ---: | ---: |
| First-pass valid | 80.0% | 100.0% | -20.0 pp |
| Eventual valid | 93.3% | 100.0% | -6.7 pp |
| Avg initial input tokens | 182.9 | 138.9 | +44.0 |
| Avg output tokens | 116.4 | 79.3 | +37.1 |
| Avg repair input tokens | 119.2 | 0.0 | +119.2 |
| Avg repair output tokens | 47.1 | 0.0 | +47.1 |
| Avg total repair tokens | 166.3 | 0.0 | +166.3 |
| Avg tokens to valid diagram | 465.6 | 218.2 | +247.4 |
| Median tokens to valid diagram | 288 | 216 | +72 |
| Max tokens to valid diagram | 1830 | 262 | +1568 |

| Category | Enzo first-pass | Enzo final | Enzo avg TTV | Mermaid avg TTV |
| --- | ---: | ---: | ---: | ---: |
| flow | 50.0% | 80.0% | 772.7 | 223.8 |
| process | 90.0% | 100.0% | 337.8 | 218.0 |
| sequence | 100.0% | 100.0% | 286.2 | 212.8 |

## Failure Inventory
All token columns are measured from persisted attempt usage. `TTV` means Tokens to Valid Diagram.

| Scenario | Category | Complexity | Initial in/out | Repairs | Repair in/out | TTV | Final | Classification |
| --- | --- | --- | ---: | ---: | ---: | ---: | --- | --- |
| `flow-password-reset` | flow | medium | 184/140 | 3 | 1086/420 | 1830 | invalid, render failed | Unquoted multiword edge labels; repair repeated same invalid source. |
| `flow-content-review` | flow | simple | 179/97 | 3 | 894/293 | 1463 | invalid, render failed | BPMN cycle; repairs oscillated between two cyclic forms. |
| `flow-warehouse-wave` | flow | complex | 191/253 | 1 | 451/255 | 1150 | valid | Undefined node `From`; repair fixed declaration/reference order and start edge. |
| `flow-data-import` | flow | complex | 188/178 | 1 | 444/174 | 984 | valid | Mermaid-style pipe labels `->|yes|`; repair converted to Enzo `: label`. |
| `flow-loan-application` | flow | complex | 188/135 | 1 | 368/139 | 830 | valid | Invalid identifier/reserved-looking `decision`; repair renamed to `finalDecision`. |
| `process-manufacturing-quality` | process | complex | 185/132 | 1 | 332/132 | 781 | valid | BPMN cycle through rework; repair removed loop. |

### Failure Transcripts
`flow-password-reset`: request was "Reset a password by identifying the account, verifying the token, checking expiry, saving the new password, and rejecting invalid attempts." Initial and all three repair outputs were identical.
```text
Attempt 0, 184 input + 140 output, invalid:
Unexpected token 'token' at end of line.; Unexpected token 'token' at end of line.; Unexpected token 'expiry' at end of line.; Unexpected token 'token' at end of line.

bpmn PasswordResetProcess
start startEvent
task identifyAccount "Identify Account"
task verifyToken "Verify Token"
task checkExpiry "Check Expiry"
task saveNewPassword "Save New Password"
task rejectInvalidAttempt "Reject Invalid Attempt"
end endEvent
gateway tokenValid "Is Token Valid?"
gateway expiryValid "Is Expiry Valid?"
startEvent -> identifyAccount
identifyAccount -> verifyToken
verifyToken -> tokenValid
tokenValid -> checkExpiry : valid token
tokenValid -> rejectInvalidAttempt : invalid token
checkExpiry -> expiryValid
expiryValid -> saveNewPassword : valid expiry
expiryValid -> rejectInvalidAttempt : expired token
saveNewPassword -> endEvent

Attempts 1, 2, and 3, each 362 input + 140 output, invalid: same source and same error.
Final result: invalid; render failed.
```

`flow-content-review`: initial source used `requestRevisions -> reviewContent`, creating a BPMN cycle. Repairs alternated between two invalid cycles.
```text
Attempt 0, 179 input + 97 output, invalid: cycle involving 'reviewContent'.
Attempt 1, 297 input + 98 output, invalid: changed requestRevisions -> decideEdits; cycle involving 'decideEdits'.
Attempt 2, 300 input + 97 output, invalid: reverted to requestRevisions -> reviewContent; cycle involving 'reviewContent'.
Attempt 3, 297 input + 98 output, invalid: changed again to requestRevisions -> decideEdits; cycle involving 'decideEdits'.
Final result: invalid; render failed.
```

`flow-data-import`: initial source was valid-looking BPMN except four Mermaid pipe-labeled edges.
```text
Attempt 0 error: Unexpected character '|'. repeated eight times, plus unexpected trailing target tokens.
Invalid lines: decisionSchemaValid ->|yes| transformRecords; decisionSchemaValid ->|no| reportResult; decisionDuplicatesFound ->|yes| reportResult; decisionDuplicatesFound ->|no| loadRecords
Attempt 1, 444 input + 174 output, valid: repaired those lines to `source -> target : yes/no`.
Final result: valid; render succeeded.
```

`flow-loan-application`: initial source declared `gateway decision "Final Decision?"` and referenced `decision`.
```text
Attempt 0, 188 input + 135 output, invalid: Expected BPMN element identifier.; Expected quoted BPMN element label.; Unexpected token 'decision'...
Attempt 1, 368 input + 139 output, valid: renamed the gateway to `finalDecision` and updated all references.
Final result: valid; render succeeded.
```

`flow-warehouse-wave`: initial source used `From -> planning` and declared `end endEvent` after the sequence flows.
```text
Attempt 0, 191 input + 253 output, invalid: Unknown BPMN element 'From' referenced by sequence flow on line 18.
Attempt 1, 451 input + 255 output, valid: added `end endEvent` before flows and replaced `From -> planning` with `startEvent -> planning`.
Final result: valid; render succeeded.
```

`process-manufacturing-quality`: initial source modeled rework as a loop back to inspection.
```text
Attempt 0, 185 input + 132 output, invalid: BPMN diagram 'ManufacturingQualityWorkflow' contains a cycle involving element 'inspection'.
Attempt 1, 332 input + 132 output, valid: changed `rework -> inspection` to `rework -> packaging`.
Final result: valid; render succeeded.
```

## Failure Classification
| Failure category | Runs | Scenarios | Evidence |
| --- | ---: | --- | --- |
| BPMN cycle from natural-language loop/rework concept | 2 | `flow-content-review`, `process-manufacturing-quality` | Validator rejected cycles; one repaired, one oscillated. |
| Label syntax problem | 2 | `flow-password-reset`, `flow-data-import` | Unquoted multiword labels and Mermaid pipe labels caused parser errors. |
| Invalid identifier or reserved keyword collision | 1 | `flow-loan-application` | `gateway decision` was rejected; `finalDecision` fixed it. |
| Undefined node reference | 1 | `flow-warehouse-wave` | `From` was never declared; replacing with `startEvent` fixed it. |
| Repair failed to address original error | 2 | `flow-password-reset`, `flow-content-review` | Password reset repeated identical invalid source; content review alternated between two cycles. |
| Excessive/unnecessary generated structure | 2 likely | `flow-warehouse-wave`, `flow-data-import` | Generated Enzo was far larger than Mermaid but still underrepresented fixture complexity. |

No Enzo failures involved Markdown/code fences, duplicate identifiers, missing participant declarations, or renderer-only failures after a syntactically valid parse. Rendering failed when validation failed.

## 1,830-Token Outlier Analysis
Sequence of the `flow-password-reset` run:
```text
Natural-language request
Reset a password by identifying the account, verifying the token, checking expiry, saving the new password, and rejecting invalid attempts.
        v
Initial generation: 184 input + 140 output = 324 tokens
        v
Validation error
Unexpected token 'token' at end of line.; Unexpected token 'token' at end of line.; Unexpected token 'expiry' at end of line.; Unexpected token 'token' at end of line.
        v
Repair 1: 362 input + 140 output = 502 tokens
        v
Same validation error; source unchanged.
        v
Repair 2: 362 input + 140 output = 502 tokens
        v
Same validation error; source unchanged.
        v
Repair 3: 362 input + 140 output = 502 tokens
        v
Final result: invalid; render failed.
```

| Component | Tokens | Share |
| --- | ---: | ---: |
| Initial generation input | 184 | 10.1% |
| Initial generation output | 140 | 7.7% |
| Repair prompt input | 1086 | 59.3% |
| Repair output | 420 | 23.0% |
| Total | 1830 | 100.0% |

Dominant cause: repair strategy plus model misunderstanding. The initial DSL error was small: branch labels such as `valid token` needed quoting or shortening. The repair prompt resent the full invalid source and repeated parser errors, but the model returned the same invalid source three times. The largest measured cost was repeated repair input, followed by repeated invalid repair output.

## Generated-Source Size Analysis
The static benchmark showed Enzo and Mermaid fixtures roughly equal: Enzo 148.8 tokens average, Mermaid 147.0. The generated benchmark differed because both models generated shorter-than-fixture source, but Mermaid compressed much more.

| Group | Ref Enzo tokens | Gen Enzo output | Ref Mermaid tokens | Gen Mermaid output | Gen Enzo - Gen Mermaid |
| --- | ---: | ---: | ---: | ---: | ---: |
| flow | 183.1 | 136.4 | 172.0 | 83.9 | +52.5 |
| process | 137.2 | 108.6 | 130.6 | 79.2 | +29.4 |
| sequence | 126.2 | 104.1 | 138.4 | 74.7 | +29.4 |
| overall | 148.8 | 116.4 | 147.0 | 79.3 | +37.1 |

| Group | Gen Enzo chars | Gen Mermaid chars | Ref Enzo chars | Ref Mermaid chars | Gen Enzo edges | Gen Mermaid edges | Ref edges |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| flow | 639.4 | 372.4 | 701.6 | 628.3 | 9.4 | 7.9 | 15.0 |
| process | 521.2 | 364.4 | 568.3 | 522.7 | 7.9 | 7.8 | 11.9 |
| sequence | 510.0 | 345.8 | 575.4 | 554.2 | 7.3 | 6.7 | 12.7 |
| overall | 556.9 | 360.9 | 615.1 | 568.4 | 8.2 | 7.5 | 13.2 |

| Scenario | Enzo output | Mermaid output | Gap | Observed cause |
| --- | ---: | ---: | ---: | --- |
| `flow-warehouse-wave` | 253 | 110 | +143 | Enzo generated six gateways and labeled branches; Mermaid generated a short linear flow and underrepresented the >40-step request. |
| `flow-data-import` | 178 | 70 | +108 | Enzo generated declarations and branch structure; Mermaid generated compact flow source. |
| `sequence-order-checkout` | 148 | 60 | +88 | Enzo generated BPMN with gateways; Mermaid generated a single chained flowchart path. |
| `sequence-travel-booking` | 143 | 58 | +85 | Enzo generated BPMN decisions; Mermaid generated a single chained flowchart path. |
| `process-procurement` | 148 | 78 | +70 | Enzo used full BPMN declarations and labels; Mermaid used shorter flowchart forms. |

Likely explanations: Mermaid uses syntax shortcuts and chained arrows; Enzo generated many BPMN diagrams regardless of category; Mermaid often produced semantically thinner diagrams; Enzo labels were often title-cased and repeated prompt wording; the benchmark validates syntax/rendering, not equivalence to fixtures.

## Instruction-Overhead Analysis
Measured input token difference was exactly +44 Enzo tokens in all 30 scenario pairs. Since the user scenario prompt is the same for both languages, the difference comes from the system prompt and chat framing around it.

| Prompt | Characters | Non-empty lines | Words | Measured initial input effect |
| --- | ---: | ---: | ---: | ---: |
| Enzo system prompt | 633 | 26 | 105 | +44 tokens versus Mermaid |
| Mermaid system prompt | 492 | 17 | 65 | baseline |

Necessary Enzo instructions: return source only, no Markdown, diagram kind selection, syntax examples for flow/sequence/BPMN, identifier rule, declaration-before-reference rule.

Potentially redundant or compressible instructions: duplicated flow and BPMN examples, repeated `From -> To` examples, optional `participant Id "Display Name"`, and flow `start/end` label examples if shorter labels are desired later.

Measured impact: instruction overhead is 44.0 tokens of the 247.4-token disadvantage, or 17.8%. It matters, but repairs are the larger optimization target.

## Successful Enzo Cases
| Scenario | Category | Enzo output | Mermaid output | Enzo TTV | Mermaid TTV | Gap |
| --- | --- | ---: | ---: | ---: | ---: | ---: |
| `sequence-cache-lookup` | sequence | 79 | 91 | 260 | 228 | +32 |
| `process-subscription-lifecycle` | process | 104 | 112 | 287 | 251 | +36 |
| `sequence-release-orchestration` | sequence | 102 | 106 | 286 | 246 | +40 |
| `sequence-login-basic` | sequence | 66 | 69 | 247 | 206 | +41 |

Enzo never beat Mermaid on total TTV because even the best output-token cases still paid the fixed +44 initial input overhead. Three scenarios had smaller Enzo output than Mermaid: `sequence-cache-lookup`, `process-subscription-lifecycle`, and `sequence-release-orchestration`; `sequence-login-basic` was also close at -3 output tokens.

What successful cases have in common: first-pass validity, short labels, no invalid multiword unquoted branch labels, no Mermaid pipe syntax, no undefined references, and either no cycles or cycles avoided in the generated control flow. The actual `sequence` DSL appeared only once in Enzo initial output (`sequence-login-basic`) and was valid with 66 output tokens versus Mermaid's 69, aligning with the static sequence-size advantage. The broader sequence-category result is confounded because most generated sources were not sequence diagrams.

## DSL Construct Failure Analysis
Construct counts are from initial Enzo generated source, with failure meaning first-pass validation failed. This 30-run dataset is small, so these are directional correlations, not statistically conclusive rates.

| Construct or pattern | Uses | First-pass failures | Failure rate |
| --- | ---: | ---: | ---: |
| `flow` diagram kind | 3 | 0 | 0.0% |
| `bpmn` diagram kind | 26 | 6 | 23.1% |
| `sequence` diagram kind | 1 | 0 | 0.0% |
| `gateway` | 17 | 6 | 35.3% |
| `decision` | 3 | 1 | 33.3% |
| Edge labels | 20 | 5 | 25.0% |
| Quoted edge labels | 15 | 4 | 26.7% |
| Multiword unquoted edge labels | 4 | 1 | 25.0% |
| Mermaid pipe labels `->|...|` | 1 | 1 | 100.0% |
| Cycle validation error | 2 | 2 | 100.0% |
| `actor`/`participant` | 1 | 0 | 0.0% |

The most meaningful observed correlations are BPMN gateways/edge labels, Mermaid-style label transfer, and cycle validation. `actor` and `participant` cannot be evaluated because only one Enzo initial output used sequence syntax.

## Complexity Analysis
| Complexity | Enzo runs | Enzo first-pass | Enzo final | Enzo output | Enzo repair total | Enzo TTV | Mermaid output | Mermaid TTV |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| simple | 9 | 88.9% | 88.9% | 87.6 | 131.9 | 399.6 | 69.3 | 205.4 |
| medium | 9 | 88.9% | 88.9% | 117.1 | 167.4 | 466.7 | 72.0 | 210.2 |
| complex | 12 | 66.7% | 100.0% | 137.4 | 191.2 | 514.2 | 92.2 | 233.8 |

Increasing complexity increased Enzo output size, repair frequency, and repair size. However, simple and medium were also expensive because each had one final failed flow run that consumed all three repairs. Mermaid output grew modestly with complexity and required no repairs.

## Token-Cost Decomposition
```text
Enzo disadvantage = 247.4 average tokens
  = 44.0 instruction/input overhead
  + 37.1 larger initial generated output
  + 119.2 repair prompt input
  + 47.1 repair output
```

| Component | Avg token gap | Share of gap |
| --- | ---: | ---: |
| Instruction/input overhead | 44.0 | 17.8% |
| Larger initial output | 37.1 | 15.0% |
| Repair prompt input | 119.2 | 48.2% |
| Repair output | 47.1 | 19.0% |
| Total | 247.4 | 100.0% |

Optimization implication: fixing first-pass validity and repair-loop behavior should come before source-size micro-optimizations. Removing all Enzo repairs in this dataset would lower Enzo average TTV from 465.6 to 299.3 before any prompt or grammar changes.

## Ranked Optimization Opportunities
| Priority | Problem | Evidence | Impact | Likely area | Risk | Recommended experiment |
| ---: | --- | --- | --- | --- | --- | --- |
| 1 | Repair loop fails to converge on small syntax errors. | `flow-password-reset` consumed 1,506 repair tokens and returned identical invalid source three times; `flow-content-review` consumed 1,187 repair tokens and oscillated. | High | Repair loop, validator error presentation, repair prompt | Medium | Replay persisted invalid sources through alternative repair prompts offline or in a small controlled live benchmark. |
| 2 | First-pass BPMN/flow generation errors. | All six first-pass failures were BPMN-style outputs; flow first-pass validity was 50%. | High | Enzo system prompt and DSL guidance, not grammar yet | Medium | Test prompt wording for valid branch labels, no Mermaid `->|label|`, no unquoted multiword edge labels, no `decision` identifier, declared starts, and acyclic BPMN. |
| 3 | Benchmark accepts wrong diagram kind and under-specified diagrams. | Enzo generated `bpmn` for 26 of 30; Mermaid generated `flowchart` for 28 of 30; most sequence-category runs were not sequence diagrams. | High for measurement accuracy | Benchmark infrastructure/scenario validation | Medium | Add diagnostic-only kind and completeness reporting before deciding whether future benchmarks require kind alignment. |
| 4 | Generated Mermaid uses compact shortcuts that Enzo cannot match in current syntax. | Mermaid often chained paths on one line; `sequence-order-checkout` was 148 Enzo output tokens versus 60 Mermaid. | Medium to high, downstream of reliability | Grammar or prompt | High if grammar changes are considered | After reliability fixes, compare minimal valid Enzo style against current generated style. |
| 5 | Fixed instruction overhead. | Enzo initial input was exactly +44 tokens in every scenario. | Medium | Enzo system prompt | Low to medium | Compress only after reliability improves, with A/B validation to avoid first-pass regression. |

## Recommended Next Experiment
Run a diagnostic prompt/repair experiment on a new branch with no grammar/parser/renderer changes:

1. Add benchmark reporting for requested category versus generated diagram kind and semantic completeness indicators.
2. Test a repair prompt variant that highlights the exact invalid line and the target Enzo syntax, while avoiding repeated full-source churn where possible.
3. Test an Enzo generation prompt variant focused on the six observed failure modes.
4. Rerun the same 30 scenarios with enough repeats to reduce noise, then compare first-pass validity, eventual validity, and TTV.

## Benchmark Success Criteria
| Metric | Required target | Stretch target |
| --- | --- | --- |
| Eventual validity | >= 99% | 100% on standard benchmark suite |
| First-pass validity | within 1-2 percentage points of Mermaid | equal to Mermaid |
| Primary cost metric | Enzo average TTV < Mermaid average TTV | Enzo at least 20% cheaper than Mermaid |
| Measurement quality | Report generated diagram kind and repair count | Enforce or separately score semantic/kind equivalence |

Do not weaken the primary objective because of the current baseline. The evidence indicates the first optimization target should be repair and first-pass validity for BPMN/flow-style Enzo output, followed by source-size and instruction-overhead reductions.
