# Historical Azure Deployment

Enzo Diagrams was previously deployed and tested successfully on Azure Container Apps using GitHub Actions, GHCR, and Azure OIDC authentication.

That hosted deployment has since been stopped. The repository no longer contains a GitHub Actions workflow that deploys the API to Azure, and the former hosted endpoint should not be treated as supported or expected to be online.

## What Previously Worked

The retired deployment path was:

```text
main
  -> GitHub Actions
  -> Docker image
  -> GHCR
  -> Azure Container Apps
```

The workflow built the ASP.NET Core API image from `src/Enzo.Diagrams.Api/Dockerfile`, pushed SHA-tagged images to GHCR, authenticated to Azure through OIDC, updated an existing Container App, and verified the deployed revision.

Known historical Azure identifiers included:

- Resource group: `rg-enzo-diagrams`
- Container App: `enzo-diagrams-api`
- GHCR image: `ghcr.io/solakpetri/enzo-diagrams-api`

These names are retained here only for historical context. They are not current deployment targets.

## Current Status

Enzo remains self-hostable as a containerized ASP.NET Core API. The Dockerfile exposes port `8080`, and local development can run the API directly with `dotnet run`.

Hosted render URL delivery remains an API capability for self-hosted deployments. Development and tests use local temporary storage by default. Self-hosted operators are responsible for configuring any external storage, expiration, cleanup, and access controls appropriate for their environment.

## External Cleanup

Repository changes do not delete cloud resources. After this branch is merged, the Azure/GitHub account cleanup remains manual:

1. Stop or delete the Azure Container App.
2. Remove the Azure federated credential used by GitHub Actions.
3. Remove the Azure service principal or app registration if no longer used.
4. Remove Azure-related GitHub secrets and variables.
5. Review GHCR images and retain or delete them based on whether container self-hosting is still useful.

Do not remove NuGet Trusted Publishing configuration if CLI package publishing remains active.
