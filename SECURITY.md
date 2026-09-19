# Security Policy

## Supported Versions

Security fixes are handled for the latest code on `main` and the latest published CLI package version when one exists.

## Reporting A Vulnerability

Do not report vulnerabilities publicly in issues or discussions.

Use GitHub private vulnerability reporting if it is enabled for this repository. If it is not enabled, contact the maintainer through a private channel already associated with the project or open a public issue requesting a private reporting path without including vulnerability details.

Do not include real API keys, cloud credentials, or other secrets in reports.

## API Authentication

`POST /v1/validate` and `POST /v1/render` require an API key in the `X-API-Key` header. Missing, empty, duplicate, or incorrect keys return `401 Unauthorized` with a concise ProblemDetails response.

Configure the key through .NET configuration as `Enzo:ApiKey` or the environment variable `Enzo__ApiKey`. Production startup fails when the key is missing or blank. Do not commit real API keys.

## SVG And PNG Safety

User-controlled labels and messages flow from DSL source into SVG and PNG output. SVG renderers encode user text so payloads such as `<script>`, `<foreignObject>`, event-handler attributes, `javascript:`, quotes, ampersands, and closing `</text>` fragments cannot become executable SVG markup.

The API also sends `X-Content-Type-Options: nosniff` on rendered images and a restrictive Content Security Policy on SVG responses.

PNG rendering rasterizes generated SVG through Infrastructure. It enforces configured width, height, and total-pixel limits to reduce CPU and memory abuse.

## Input Limits

The API enforces configurable limits for:

- request body size
- source character count
- source line count
- diagram element count
- diagram connection/message count
- PNG width, height, and total pixels

See [API authentication](docs/api-authentication.md#api-limits) for default values and error codes.

## Hosted Render Security

Hosted PNG URL delivery remains supported for self-hosted deployments. The default local store creates opaque 32-character lowercase hex identifiers, records expiration metadata, and serves stored images through `GET /v1/render-results/{id}` without requiring callers to expose `X-API-Key` to browsers or end users.

Self-hosted operators are responsible for configuring storage location, retention, cleanup, access controls, and any external storage provider. Azure Blob storage exists as an optional Infrastructure implementation, but the repository no longer automatically deploys to Azure and does not document an active production Azure endpoint.

## Security-Sensitive Areas

Reports are especially useful for:

- DSL parser or validator denial-of-service cases.
- SVG/XML injection through user-controlled labels or messages.
- PNG rendering CPU, memory, or image-size abuse.
- API authentication bypasses.
- Hosted render URL predictability, expiration, cleanup, or storage exposure.
- Error responses that expose stack traces, local paths, environment values, API keys, connection strings, or secrets.
