# ChatGPT App

Enzo.Diagrams exposes a minimal ChatGPT Apps SDK component for the remote MCP `render_diagram` tool. The component renders the exact Enzo-generated PNG returned by the tool as an inline `<img>` inside ChatGPT.

It does not use OpenAI image generation, Mermaid, Graphviz, browser-side SVG drawing, or a separate chat frontend.

## Endpoint

Use the public HTTPS MCP endpoint for the deployed MCP server:

```text
https://<your-public-mcp-host>/mcp
```

For local development, run the MCP server and expose it through an HTTPS tunnel that ChatGPT can reach:

```powershell
dotnet run --project src/Enzo.Diagrams.Mcp --urls http://localhost:5095
```

The local MCP path is `http://localhost:5095/mcp`; in ChatGPT, use the tunnel URL with the same `/mcp` path, for example `https://<your-tunnel-host>/mcp`.

## ChatGPT Setup

1. Open ChatGPT.
2. Go to `Settings` -> `Apps` -> `Create`.
3. Set the MCP server URL to `https://<your-public-mcp-host>/mcp`.
4. Do not paste API keys or secrets into the app description, endpoint, or test prompts.
5. Scan the server tools.
6. Confirm `render_diagram` is available and advertises `ui://widget/enzo-diagram.html` as its output template.

## Testing

Use this prompt in ChatGPT after the app is connected:

```text
generate a sequence diagram of pizza making process
```

Expected result:

1. ChatGPT calls `render_diagram` with valid Enzo.Diagrams DSL.
2. Enzo renders a PNG server-side.
3. The ChatGPT app result visibly displays the PNG in the conversation.
4. The result is not just a clickable URL.

If ChatGPT shows only a link or text, the acceptance criterion is not met. Re-scan the app and verify the `render_diagram` descriptor includes `openai/outputTemplate` and that the `ui://widget/enzo-diagram.html` resource can be read.
