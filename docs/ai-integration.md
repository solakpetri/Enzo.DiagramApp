# AI Integration

Enzo.Diagrams is deterministic and AI-provider-independent. AI agents can generate Enzo.Diagrams DSL and call the HTTP API, but the repository does not include an LLM SDK, provider dependency, authentication layer, database, or frontend.

Use the checked-in agent contract at [`docs/openapi/agent.openapi.json`](openapi/agent.openapi.json) for OpenAPI-capable agents.

Public demo API:

```text
https://enzo-diagrams-api.gentlebeach-2a13ea58.northeurope.azurecontainerapps.io
```

## Agent Flow

1. Determine whether the requested diagram should be `flow`, `sequence`, or `bpmn`.
2. Generate only Enzo.Diagrams DSL.
3. Do not use Mermaid, PlantUML, Graphviz, BPMN XML, raw SVG, or image bytes.
4. For non-trivial diagrams, call `validateDiagram` with `POST /v1/validate`.
5. If validation fails, correct the DSL and validate again.
6. Only call `renderDiagram` with `POST /v1/render` after valid DSL has been produced.
7. Prefer SVG unless the user explicitly asks for PNG.

```text
Natural language
       ↓
      AI
       ↓
Enzo.Diagrams DSL
       ↓
 /v1/validate
       ↓
  /v1/render
       ↓
      SVG
```

## Agent Instructions

Copy this block into a custom GPT, coding agent, or OpenAPI-capable assistant:

```text
You create diagrams with Enzo.Diagrams.

Generate only Enzo.Diagrams DSL. Do not generate Mermaid, PlantUML, Graphviz, BPMN XML, raw SVG, or image bytes.

Choose exactly one top-level diagram type:
- flow for flowcharts
- sequence for sequence diagrams
- bpmn for the supported BPMN-inspired subset

For non-trivial diagrams, call validateDiagram before rendering. If validation fails, use the returned errors to correct the DSL and call validateDiagram again. Call renderDiagram only after valid DSL has been produced. Prefer format svg unless the user explicitly asks for png.

Flow DSL uses declarations start, task, decision, and end. Connections use Source -> Target, with optional labels as Source -> Target : label. The keyword node is invalid. Mermaid-style square-bracket node syntax must not be used.

Sequence DSL uses actor and participant declarations. Messages use Source -> Target: label, with --> allowed for response-style messages.

BPMN DSL uses start, task, gateway, and end declarations. Sequence flows use Source -> Target, with optional labels as Source -> Target : label.
```

## DSL Examples

Flowchart:

```text
flow Example

start Begin "Start"
task Work "Process"
decision Valid "Valid?"
end Done "Done"

Begin -> Work
Work -> Valid
Valid -> Done : yes
```

Supported flow declarations are `start`, `task`, `decision`, and `end`. Flow connections use `Source -> Target`; optional edge labels use `Source -> Target : label`. The keyword `node` is invalid. Mermaid-style square-bracket node syntax such as `A[Start]` must not be used.

Sequence diagram:

```text
sequence Checkout

actor Customer "Customer"
participant Web "Web App"
participant Api "Order API"

Customer -> Web: Click checkout
Web -> Api: POST /orders
Api --> Web: 201 Created
```

Sequence diagrams use `actor` and `participant`. Messages use `->`; `-->` is allowed for response-style messages.

BPMN subset:

```text
bpmn Fulfillment

start Received
task Validate "Validate order"
gateway Valid "Order valid?"
task Ship "Ship order"
end Completed

Received -> Validate
Validate -> Valid
Valid -> Ship : yes
Ship -> Completed
```

Supported BPMN declarations are `start`, `task`, `gateway`, and `end`. BPMN sequence flows use `Source -> Target`; optional edge labels use `Source -> Target : label`.

## ChatGPT Action Setup

For ChatGPT Actions or another OpenAPI-capable agent:

1. Create or configure an Action/tool in the agent provider.
2. Use the checked-in schema from [`docs/openapi/agent.openapi.json`](openapi/agent.openapi.json).
3. Set authentication to `None` for the current public demo endpoint.
4. Test `validateDiagram` with a valid Enzo.Diagrams DSL sample.
5. Test `renderDiagram` with the same DSL and `format` set to `svg`.
6. Add the instruction block above so the agent validates before rendering and avoids other diagram syntaxes.

Authentication is intentionally not implemented in this branch and will be handled separately. The public unauthenticated endpoint is a demo endpoint; do not treat it as production-secure.

## API Contract Notes

The agent contract is version-controlled and uses the public HTTPS server URL. The API also exposes generated OpenAPI at `GET /openapi/v1.json` for runtime inspection.

Invalid JSON, missing fields, unsupported formats, parser failures, validation failures, and PNG rasterization failures return `application/problem+json` with an `errors` extension that agents can use to repair DSL.

## Limitations

- No direct ChatGPT, OpenAI, or LLM integration is packaged in this repository.
- No authentication is implemented for the public demo endpoint yet.
- Enzo.Diagrams does not accept Mermaid, PlantUML, Graphviz, BPMN XML, raw SVG, or image input.
- BPMN support is a small inspired subset, not BPMN 2.0 compliance.
- Layout is automatic; the API does not currently accept layout hints.
