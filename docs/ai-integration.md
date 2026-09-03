# AI Agent Integration

Enzo.Diagrams is AI-provider-independent. An AI agent can generate Enzo.Diagrams DSL and call the HTTP API, but Enzo.Diagrams does not call an LLM and does not require API keys.

## Algorithm

1. Determine which supported diagram type best matches the request: flowchart, sequence diagram, or the supported BPMN subset.
2. Generate valid Enzo.Diagrams DSL.
3. Do not generate Mermaid, PlantUML, Graphviz, raw SVG, BPMN XML, or PNG bytes.
4. Submit the DSL to `POST /v1/render` with `format` set to `svg` or `png`.
5. If parsing or validation fails, inspect the returned `application/problem+json` response and its `errors` extension.
6. Correct the DSL.
7. Retry rendering.
8. Present the resulting `image/svg+xml` or `image/png` response to the user.

Use `POST /v1/validate` when you want to check DSL before rendering.

## HTTP API

Validation request:

```json
{
  "source": "flow Checkout\nstart Begin \"Order received\"\nend Complete \"Complete order\"\nBegin -> Complete"
}
```

Validation success response:

```json
{
  "valid": true
}
```

Render request:

```json
{
  "source": "flow Checkout\nstart Begin \"Order received\"\nend Complete \"Complete order\"\nBegin -> Complete",
  "format": "svg"
}
```

Supported render formats are `svg` and `png`. Successful SVG responses use `image/svg+xml`. Successful PNG responses use `image/png`.

Invalid JSON, missing `source`, missing `format`, unsupported `format`, parser failures, validation failures, and PNG rasterization failures return `application/problem+json`. The response includes an `errors` extension with entries shaped like:

```json
{
  "type": "syntax",
  "line": 2,
  "column": 1,
  "message": "Expected ...",
  "code": null
}
```

`type` can include `syntax`, `validation`, `request`, or `rendering`. Validation errors can include a machine-readable `code`.

The generated OpenAPI document is available from the API through `GET /openapi/v1.json`.

## Shared DSL Rules

The first declaration selects the diagram type:

- `flow <Name>` for flowcharts.
- `sequence <Name>` for sequence diagrams.
- `bpmn <Name>` for the supported BPMN subset.

Identifiers must start with an ASCII letter or `_`, followed by ASCII letters, digits, or `_`. Keywords are lowercase and case-sensitive. Quoted labels use double quotes and cannot span multiple lines. The DSL has no comment syntax.

## Flowcharts

Use flowcharts for process, decision, and task diagrams.

Supported node declarations:

- `start <Id> "Label"`
- `task <Id> "Label"`
- `decision <Id> "Label"`
- `end <Id> "Label"`

Edges use `<From> -> <To>` and may include an optional label after `:`.

```text
flow Checkout
start Begin "Order received"
task Validate "Validate order"
decision Available "Stock available?"
task Reserve "Reserve stock"
end Complete "Complete order"
end Reject "Reject order"
Begin -> Validate
Validate -> Available
Available -> Reserve : yes
Available -> Reject : no
Reserve -> Complete
```

## Sequence Diagrams

Use sequence diagrams for participants exchanging ordered messages.

Supported participants:

- `actor <Id>`
- `participant <Id>`

Messages use `<From> -> <To>: Label` for calls and `<From> --> <To>: Label` for responses. Every message requires a label after `:`.

```text
sequence Checkout
actor Customer
participant API
participant Payment
Customer -> API: Checkout
API -> Payment: Charge
Payment --> API: Success
API --> Customer: Confirmed
```

## BPMN Subset

Use the BPMN subset only for simple BPMN-inspired workflows. It is not full BPMN 2.0.

Supported element declarations:

- `start <Id>`
- `task <Id> "Label"`
- `gateway <Id> "Label"`
- `end <Id>`

Sequence flows use `<From> -> <To>` and may include an optional label after `:`.

```text
bpmn Order
start Received
task Validate "Validate order"
gateway Available "Stock available?"
task Reserve "Reserve stock"
end Complete
Received -> Validate
Validate -> Available
Available -> Reserve : yes
Reserve -> Complete
```

## Limitations

- No direct ChatGPT, OpenAI, or LLM integration is packaged in this repository.
- Enzo.Diagrams does not accept Mermaid, PlantUML, Graphviz, raw SVG, BPMN XML, or image input.
- BPMN support is a small inspired subset, not BPMN 2.0 compliance.
- Unsupported BPMN features include pools, lanes, message events, timer events, subprocesses, imports, exports, and gateway types beyond the supported gateway syntax.
- Labels cannot contain escaped quotes or span multiple lines.
- Layout is automatic; the API does not currently accept layout hints.
