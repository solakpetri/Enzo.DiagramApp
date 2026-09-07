# Equivalent Generation Method

## Shared Contract

Each model receives the original scenario text unchanged plus a language-neutral `GenerationContract` derived from frozen scenario metadata. Applicable fields are rendered naturally: diagram kind, required concepts, and minimum node, edge, decision, participant, or interaction counts. Internal property names and validator implementation details are not exposed.

## Prompt Structure

The Enzo and Mermaid tasks share the same semantic request. Language-specific guidance is limited to source syntax and the exact declaration required for the expected kind.

Enzo examples: use `flow` for flow, `sequence` for sequence, and `bpmn` for process. Mermaid examples: use `flowchart TD` for flow/process and `sequenceDiagram` for sequence. Both prompts require source only, no Markdown fences, the requested diagram kind, all required concepts, and applicable structural minimums.

## Semantic Repair Flow

Every attempt is checked for syntax validity, render validity, expected kind, structural sufficiency, and required concepts. Repair continues until `EquivalentValid = true` or the repair budget is exhausted. The default maximum repair attempts remains `3`.

Repair feedback is concise and deterministic. It lists only failed checks, such as wrong kind, missing concepts, or insufficient structural counts, then asks for the complete corrected source only. Minimal language-specific syntax guidance is appended where needed.

## Stagnation Detection

If a repair returns source byte-for-byte identical to the previous normalized source, the run stops with `repairStoppedReason = "identical-output"`. A simple A-B-A repair cycle stops with `repairStoppedReason = "repair-oscillation"`. These unresolved runs remain in validity percentages.

## Token Accounting

Live OpenAI API token usage is authoritative. TTVED includes initial input/output tokens and every repair input/output token through the first equivalent-valid attempt. Semantic repair tokens are included exactly once. If equivalence is never reached, TTVED is `unresolved`.

`Tokens to Valid Diagram` remains a secondary diagnostic for syntax/render validity and stops at the first syntax/render-valid attempt.

## Success Rules

A successful run requires `EquivalentValid = true`: syntax-valid, render-valid, correct kind, structurally sufficient, and semantically complete. Failed or stagnated runs are not hidden, assigned zero, or excluded from equivalent-validity percentages. Average TTVED is reported as successful-run-only.

## Reporting

JSON includes full attempts, repair type, semantic diagnostics, prompt audit metadata, and stagnation reason. CSV and Markdown report syntax first-pass/eventual validity, equivalent first-pass/eventual validity, kind validity, structural completeness, semantic completeness, average TTV, average successful TTVED, unresolved count, failure rate, repair diagnostics, and breakdowns by category and complexity.

## Cost Comparison Threshold

Headline TTVED cost comparisons are only reported when both languages reach at least `95%` eventual equivalent validity. Otherwise the report states that cost comparison is unresolved because equivalent validity is below the threshold. The default threshold is persisted in prompt audit metadata.

## Manual Benchmark

```powershell
$env:OPENAI_API_KEY = "your-openai-api-key"
dotnet run --project benchmarks/Enzo.Diagrams.LlmBenchmarks -- --runs 1 --language all
```

This runs the frozen 30 scenarios once for Enzo and once for Mermaid. Do not run larger repeats until the 1-run equivalent-validity result has been reviewed.

## Interpretation

Treat TTVED as a serious comparative cost metric only when both languages reach at least `95%` eventual equivalent validity, preferably `99%` or higher. If validity remains poor, analyze prompt failures, repair feedback, and semantic diagnostics before optimizing the Enzo grammar, parser, renderer, or DSL.
