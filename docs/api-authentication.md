# API Authentication

The Enzo Diagrams HTTP API uses lightweight API-key authentication for diagram validation and rendering.

## Protected Endpoints

- `POST /v1/validate`
- `POST /v1/render`

Send the key in this request header:

```text
X-API-Key: YOUR_ENZO_API_KEY
```

Requests with a missing, empty, or incorrect key return `401 Unauthorized` with a concise ProblemDetails response.

## Public Endpoints

Generated OpenAPI remains public for integration discovery:

- `GET /openapi/v1.json`

No health endpoint exists in this branch.

## Configuration

The API key is read from .NET configuration:

```text
Enzo:ApiKey
```

Environment variable representation:

```text
Enzo__ApiKey
```

The checked-in `appsettings.json` contains an empty value only as a configuration example. Do not commit a real API key.

For local development, set the environment variable before starting the API:

```powershell
$env:Enzo__ApiKey = "YOUR_LOCAL_API_KEY"
dotnet run --project src/Enzo.Diagrams.Api --urls http://localhost:5085
```

Production startup fails if `Enzo:ApiKey` is missing or blank.

## Self-hosting

For self-hosted deployments, configure `Enzo:ApiKey` through the hosting platform's secret or environment-variable mechanism. Do not place the raw key in source-controlled configuration.

The checked-in example file `src/Enzo.Diagrams.Api/appsettings.Development.example.json` shows the local configuration shape with placeholders only.

The previous Azure Container Apps deployment is retired. See [Historical Azure Deployment](azure-container-deployment.md) for context.
