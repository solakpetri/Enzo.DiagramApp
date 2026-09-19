# Benchmarking Enzo Diagrams

This document summarizes the Enzo vs Mermaid benchmarking work. The goal was to understand where a small AI-oriented diagram DSL is useful, where it is not, and what conclusions are supported by the measured results.

Enzo Diagrams is a custom diagram DSL designed to be easy for AI systems to generate, validate, and render. Mermaid is a mature, established, broad diagramming language. The benchmark was not intended to prove that Mermaid is bad; it was intended to evaluate whether Enzo offers useful trade-offs in narrower AI-generation workflows.

## Hypothesis

The original hypothesis was:

> A smaller, AI-oriented DSL may require fewer tokens for an LLM to generate while still producing valid and semantically equivalent diagrams.

The project initially benchmarked flow, process/BPMN, and sequence diagrams. That broad benchmark did not show a general Enzo advantage. Flow and process generation introduced substantial repair and reliability overhead, while sequence diagrams consistently showed the most promising results.

The final benchmark therefore focused specifically on:

```text
Enzo `sequence`
vs
Mermaid `sequenceDiagram`
```

## Benchmark Evolution

### 1. Static DSL Comparison

The first benchmark compared 30 equivalent hand-written diagrams:

- 10 flow diagrams
- 10 process diagrams
- 10 sequence diagrams

Overall token count:

```text
Enzo    4465
Mermaid 4410
Enzo used approximately 1.2% more tokens overall.
```

By category:

```text
Flow:     Enzo approximately 6.5% more tokens
Process:  Enzo approximately 5.1% more tokens
Sequence: Enzo approximately 8.8% fewer tokens
```

Conclusion: the custom DSL was not more compact overall. Sequence syntax was the strongest candidate for further testing.

### 2. LLM Generation Benchmark

Later benchmarks measured LLM generation using:

```text
Model: gpt-4o-mini
Temperature: 0.2
Maximum repair attempts: 3
```

Generation cost included:

```text
initial input
+ initial output
+ repair input
+ repair output
```

The benchmark evolved from `Tokens to Valid Diagram` to `Tokens to Valid Equivalent Diagram` because syntax-valid output alone was not sufficient.

A valid-equivalent diagram must:

- Parse successfully
- Render successfully
- Use the requested diagram kind
- Include required semantic concepts
- Satisfy structural requirements

The benchmark used deterministic semantic checks rather than an LLM judge.

## Why Sequence Diagrams Became the Focus

Broad benchmarking revealed recurring difficulties in flow and process generation:

- Cycles
- Branch-label errors
- Structural omissions
- Expensive repair loops

Sequence diagrams showed a better fit for Enzo:

- Smaller Enzo source representations
- High reliability
- Simpler generation behavior
- Several runs where Enzo matched or cost less than Mermaid

This led to a dedicated 30-scenario sequence suite.

## Sequence Benchmark Methodology

The final confirmation benchmark used:

```text
30 sequence scenarios
10 simple
10 medium
10 complex
5 runs per scenario
2 languages
150 Enzo generations
150 Mermaid generations
300 total initial generations
```

Configuration:

```text
Model: gpt-4o-mini
Temperature: 0.2
Maximum repair attempts: 3
```

Repair calls were included in total token cost. The scenarios covered realistic software and integration workflows, including APIs, authentication, databases, microservices, payments, webhooks, asynchronous processing, and orchestration.

## Final Sequence Benchmark

### Reliability

| Metric | Enzo | Mermaid |
| --- | ---: | ---: |
| First-pass valid | 88.0% | 100% |
| Eventual valid | 99.3% | 100% |
| First-pass equivalent-valid | 72.7% | 85.3% |
| Eventual equivalent-valid | 99.3% | 96.7% |
| Unresolved runs | 1 / 150 | 5 / 150 |

Mermaid was substantially stronger at first-pass syntax generation. Enzo required more repairs. After repair, Enzo achieved slightly higher semantic-equivalent completion in this benchmark, but this does not prove that Enzo is generally more reliable.

### Generation Cost

| Metric | Enzo | Mermaid |
| --- | ---: | ---: |
| Avg Tokens to Valid Diagram | 436.1 | 441.6 |
| Median Tokens to Valid Diagram | 359.5 | 431 |
| Avg successful TTVED | 555.9 | 518.6 |
| Median successful TTVED | 388 | 435 |
| P90 successful TTVED | 971 | 777 |

Enzo used approximately 1.2% fewer Tokens to Valid Diagram on average. However, Enzo used 7.2% more mean successful Tokens to Valid Equivalent Diagram because it had a heavier repair-cost tail.

The median tells a different story:

```text
Median TTVED:
Enzo    388
Mermaid 435
```

The typical Enzo generation was cheaper in this benchmark, while the mean was worse because complex repairs were expensive. Both views matter.

## Source Efficiency

| Metric | Enzo | Mermaid |
| --- | ---: | ---: |
| Avg source characters | 649.9 | 755.3 |
| Avg output tokens | 149.0 | 156.7 |
| Avg output tokens / interaction | 11.3 | 11.8 |

Enzo produced approximately 14% fewer source characters and roughly 5% fewer output tokens on average. This supports the narrower hypothesis that Enzo sequence syntax is compact.

Source compactness does not directly equal lower total API cost because repair behavior can dominate total token usage.

## Results By Complexity

| Complexity | Enzo equivalent | Mermaid equivalent | Enzo TTVED | Mermaid TTVED |
| --- | ---: | ---: | ---: | ---: |
| Simple | 100% | 100% | 303.9 | 346.0 |
| Medium | 98% | 100% | 470.5 | 562.5 |
| Complex | 100% | 90% | 891.5 | 661.5 |

### Simple

Enzo used approximately 12% fewer successful TTVED tokens.

### Medium

Enzo used approximately 16% fewer successful TTVED tokens. This was the strongest cost result.

### Complex

Mermaid was substantially cheaper on mean TTVED. Enzo's repair rate became much higher:

```text
Enzo:    48%
Mermaid: 26%
```

However, equivalent validity was higher for Enzo in this benchmark:

```text
Enzo:    100%
Mermaid: 90%
```

This was an observed result in this benchmark. It should not be treated as a general reliability claim without broader testing.

## Prompt Compression

Enzo originally needed substantially more input tokens because the LLM had to be taught the custom DSL. Prompt compression reduced sequence syntax guidance to compact rules and canonical examples.

After compression, the confirmed benchmark measured:

```text
Avg initial input tokens:
Enzo:    218.9
Mermaid: 284.9
```

This reversed the previous custom-language prompt disadvantage. This matters because a custom DSL only makes sense for AI generation if teaching the language does not cost more than the resulting source savings.

## Repair Behavior

| Metric | Enzo | Mermaid |
| --- | ---: | ---: |
| Repair rate | 27.3% | 14.7% |
| Repair success rate | 80.4% | 54.8% |
| Syntax repairs | 19 | 0 |
| Semantic repairs | 21 | 8 |

Mermaid benefits from strong pretrained familiarity and generated correct syntax on the first pass much more often. Enzo's main remaining disadvantage is repair frequency, especially for complex sequence diagrams.

## What The Benchmark Shows

Supported conclusions:

- Enzo sequence source is more compact than Mermaid in this benchmark.
- Enzo achieved lower average Tokens to Valid Diagram overall.
- Enzo had a substantially lower median Tokens to Valid Diagram.
- Enzo had a lower median TTVED.
- Enzo was cheaper on simple and medium sequence diagrams.
- Mermaid remained cheaper on complex diagrams because Enzo required more repairs.
- Enzo reached 99.3% eventual semantic-equivalent validity.
- Mermaid had substantially stronger first-pass generation reliability.

## What The Benchmark Does Not Show

The benchmark does not prove:

- Enzo is generally better than Mermaid
- Enzo is cheaper for all diagram types
- Enzo is cheaper for all sequence diagrams
- These results generalize to every LLM
- These results generalize to all prompting strategies
- 30 scenarios represent every real sequence-diagram workload

The benchmark used one model:

```text
gpt-4o-mini
```

Results may differ with other models.

## Final Interpretation

The benchmark did not validate the original idea that Enzo is universally more token-efficient than Mermaid. It did show a narrower and more useful result: Enzo's sequence syntax can be generated compactly by an LLM, and for simple and medium sequence diagrams it achieved lower total equivalent-generation cost in this benchmark.

For complex sequence diagrams, Enzo's higher repair frequency outweighed its source-size advantage. The experiment therefore suggests that small AI-oriented DSLs can be competitive in focused domains, but mature languages such as Mermaid retain a significant advantage from existing model familiarity and ecosystem maturity.

## Engineering Lessons

- DSL compactness alone does not determine LLM cost.
- Prompt instruction overhead matters.
- First-pass reliability matters more than small source savings.
- Repair loops can dominate total token cost.
- Semantic correctness must be benchmarked, not only parser validity.
- Mean and median costs can tell different stories.
- Optimizing a custom DSL against a mature language is difficult because the model already knows the mature language.
