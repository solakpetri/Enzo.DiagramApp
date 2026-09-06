# AI Integration

Enzo.Diagrams is deterministic and AI-provider-independent. AI agents can generate Enzo.Diagrams DSL and call the HTTP API, but the repository does not include an LLM SDK, provider dependency, user account system, database, or frontend.

Use the checked-in agent contract at [`docs/openapi/agent.openapi.json`](openapi/agent.openapi.json) for OpenAPI-capable agents.

Public demo API:

```text
https://enzo-diagrams-api.gentlebeach-2a13ea58.northeurope.azurecontainerapps.io
```

Hosted validation and rendering requests require an API key in the `X-API-Key` header. Do not place the actual key in prompts, examples, or source-controlled configuration.

## Agent Flow

1. Determine whether the requested diagram should be `flow`, `sequence`, or `bpmn`.
2. Generate only Enzo.Diagrams DSL.
3. Do not use Mermaid, PlantUML, Graphviz, BPMN XML, raw SVG, or image bytes.
4. For non-trivial diagrams, call `validateDiagram` with `POST /v1/validate`.
5. If validation fails, correct the DSL and validate again.
6. Only call `renderDiagram` with `POST /v1/render` after valid DSL has been produced.
7. For ChatGPT inline image presentation, call `renderDiagram` with `format` set to `png` and `delivery` set to `url`.
8. Present the returned `url` directly to the user. Do not recreate, redraw, or regenerate the diagram with ChatGPT image generation.

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
 SVG / PNG URL
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

For non-trivial diagrams, call validateDiagram before rendering. If validation fails, use the returned errors to correct the DSL and call validateDiagram again. Call renderDiagram only after valid DSL has been produced. For inline ChatGPT images, call renderDiagram with format png and delivery url, then present the returned URL directly. Do not use ChatGPT image generation to recreate the diagram.

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
3. Set Authentication to `API Key`.
4. Set the header name to `X-API-Key` and provide the key through the agent provider's secret/authentication UI.
5. Test `validateDiagram` with a valid Enzo.Diagrams DSL sample.
6. Test `renderDiagram` with the same DSL and `format` set to `png` and `delivery` set to `url`.
7. Add the instruction block above so the agent validates before rendering and avoids other diagram syntaxes.

The agent flow remains:

```text
Natural language
→ generate Enzo.Diagrams DSL
→ validateDiagram
→ correct DSL if necessary
→ renderDiagram
→ present returned PNG URL when delivery is url
```

## API Contract Notes

The agent contract is version-controlled and uses the public HTTPS server URL. The API also exposes generated OpenAPI at `GET /openapi/v1.json` for runtime inspection.

The checked-in agent OpenAPI contract declares API-key authentication with the `X-API-Key` header on `validateDiagram` and `renderDiagram` only.

See [API authentication](api-authentication.md) for the `Enzo:ApiKey` configuration key and Azure Container Apps setup.

Invalid JSON, missing fields, unsupported formats, parser failures, validation failures, and PNG rasterization failures return `application/problem+json` with an `errors` extension that agents can use to repair DSL.

Hosted PNG rendering extends `POST /v1/render` without changing existing raw rendering behavior. Omit `delivery` or set it to `raw` for the existing SVG/PNG response body. Set `delivery` to `url` with `format` set to `png` to receive:

```json
{
  "id": "b6f4e81ab3d2440d8ad70c8d68a1828f",
  "format": "png",
  "contentType": "image/png",
  "url": "https://storage.example.invalid/render-results/b6f4e81ab3d2440d8ad70c8d68a1828f.png?sv=redacted",
  "expiresAt": "2026-09-06T13:30:00Z"
}
```

The hosted URL is short-lived, read-only, and does not require exposing `X-API-Key` to the browser or user. The image bytes are generated by Enzo.Diagrams itself.

## Limitations

- No direct ChatGPT, OpenAI, or LLM integration is packaged in this repository.
- Enzo.Diagrams does not accept Mermaid, PlantUML, Graphviz, BPMN XML, raw SVG, or image input.
- BPMN support is a small inspired subset, not BPMN 2.0 compliance.
- Layout is automatic; the API does not currently accept layout hints.
- Hosted URL delivery currently supports PNG only.
