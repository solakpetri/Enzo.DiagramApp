# API Authentication

The hosted Enzo.Diagrams API uses lightweight API-key authentication for diagram validation and rendering.

## Protected Endpoints

- `POST /v1/validate`
- `POST /v1/render`

Send the key in this request header:

```text
X-API-Key: <your-api-key>
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
$env:Enzo__ApiKey = "<local-development-key>"
dotnet run --project src/Enzo.Diagrams.Api --urls http://localhost:5085
```

Production startup fails if `Enzo:ApiKey` is missing or blank.

## Azure Container Apps

Configure the key as an Azure Container Apps secret and map it to the .NET configuration environment variable:

1. Create a secret such as `enzo-api-key` containing the generated API key.
2. Reference that secret from the container app environment variable `Enzo__ApiKey`.
3. Do not place the raw API key in source-controlled configuration.

Example Azure CLI shape:

```bash
az containerapp secret set --name <app-name> --resource-group <resource-group> --secrets enzo-api-key=<generated-api-key>
az containerapp update --name <app-name> --resource-group <resource-group> --set-env-vars Enzo__ApiKey=secretref:enzo-api-key
```

Key Vault is intentionally not provisioned in this branch.
