# Azure Container Deployment

The API deployment path is:

```text
main
  ↓
GitHub Actions
  ↓
GHCR
  ↓
Azure Container Apps
```

Merges to `main` run `.github/workflows/deploy-api.yml` when API container inputs change. The workflow can also be started manually with `workflow_dispatch` from `main`. Pull requests and non-`main` refs do not deploy.

## Image

The workflow builds the existing API Dockerfile from the repository root:

```bash
docker build -f src/Enzo.Diagrams.Api/Dockerfile -t enzo-diagrams-api .
```

Images are pushed to GitHub Container Registry as:

```text
ghcr.io/solakpetri/enzo-diagrams-api:<commit-sha>
ghcr.io/solakpetri/enzo-diagrams-api:latest
```

Azure Container Apps is updated to the immutable commit SHA tag, not `latest`, so the running revision can be traced back to the exact Git commit.

The GHCR package is intended to remain publicly pullable. Do not make the package private unless Azure pull authentication is added intentionally.

## GitHub Variables

Create these repository variables under **Settings -> Secrets and variables -> Actions -> Variables**:

| Variable | Value |
| --- | --- |
| `AZURE_CLIENT_ID` | Microsoft Entra application client ID |
| `AZURE_TENANT_ID` | Microsoft Entra tenant ID |
| `AZURE_SUBSCRIPTION_ID` | Azure subscription ID |
| `AZURE_RESOURCE_GROUP` | Resource group containing the existing Container App, expected `rg-enzo-diagrams` |
| `AZURE_CONTAINER_APP_NAME` | Existing Container App name, expected `enzo-diagrams-api` |

These values are identifiers, not passwords. Prefer repository variables, not secrets. Do not add Azure client secrets, publish profiles, service-principal JSON, or registry PATs.

## Azure OIDC Setup

Configure GitHub OIDC once before relying on the workflow. The deployment identity should trust this repository and should receive only the Azure permissions needed to update the existing Container App.

The current repository remote is `solakpetri/Enzo.DiagramApp`. If the repository is renamed to `solakpetri/Enzo.Diagrams`, use the renamed repository in the federated credential subject instead.

```text
GitHub repository
solakpetri/Enzo.DiagramApp

        ↓ OIDC

Microsoft Entra application /
service principal

        ↓ RBAC

Azure resource group
rg-enzo-diagrams
```

Verify the existing Azure resource names before assigning access:

```bash
az containerapp show \
  --name enzo-diagrams-api \
  --resource-group rg-enzo-diagrams \
  --query "{name:name, resourceGroup:resourceGroup, environmentId:properties.environmentId, image:properties.template.containers[0].image}" \
  --output table
```

Example Azure CLI setup:

```bash
az ad app create --display-name enzo-diagrams-api-github-deploy

appId=$(az ad app list \
  --display-name enzo-diagrams-api-github-deploy \
  --query "[0].appId" \
  --output tsv)

spObjectId=$(az ad sp create --id "$appId" --query id --output tsv)

az ad app federated-credential create \
  --id "$appId" \
  --parameters '{
    "name": "enzo-diagrams-api-main",
    "issuer": "https://token.actions.githubusercontent.com",
    "subject": "repo:solakpetri/Enzo.DiagramApp:ref:refs/heads/main",
    "audiences": ["api://AzureADTokenExchange"]
  }'

subscriptionId=$(az account show --query id --output tsv)

az role assignment create \
  --assignee-object-id "$spObjectId" \
  --assignee-principal-type ServicePrincipal \
  --role "Container Apps Contributor" \
  --scope "/subscriptions/${subscriptionId}/resourceGroups/rg-enzo-diagrams"
```

If `Container Apps Contributor` is not available in the tenant, use a custom role scoped to the resource group with permissions to read and update `Microsoft.App/containerApps` and read revisions. Avoid subscription-wide `Contributor` when resource-group scope is sufficient.

Put `appId` in `AZURE_CLIENT_ID`, the tenant ID in `AZURE_TENANT_ID`, and the subscription ID in `AZURE_SUBSCRIPTION_ID`.

## What The Workflow Updates

The workflow runs:

```bash
az containerapp update \
  --name "$AZURE_CONTAINER_APP_NAME" \
  --resource-group "$AZURE_RESOURCE_GROUP" \
  --image "$IMAGE_TAG"
```

It does not recreate the resource group, Container Apps environment, or Container App. Updating only the image preserves existing Azure-owned configuration such as secrets, `Enzo__ApiKey`, ingress, target port `8080`, scaling limits, replica settings, and the Consumption workload profile.

## Hosted Render Storage

Hosted PNG delivery uses temporary read-only image URLs. Development and tests default to local temp-file storage, but Azure deployments should use private Azure Blob Storage with short-lived SAS URLs.

Required Container App settings for Azure Blob hosted rendering:

| Setting | Purpose |
| --- | --- |
| `Enzo__RenderResults__Store=AzureBlob` | Enables Azure Blob-backed hosted render results. |
| `Enzo__RenderResults__BlobContainerName=<container-name>` | Private container for temporary PNG blobs. |
| `Enzo__RenderResults__BlobConnectionString=secretref:<secret-name>` | Storage connection string supplied through a Container Apps secret. |
| `Enzo__RenderResults__UrlLifetimeMinutes=30` | Read-only URL lifetime. Valid range is 1-60 minutes; 30 is the default. |

Create a private container and map the connection string through a secret. Do not make the container public.

```bash
az storage container create \
  --name <container-name> \
  --connection-string "<development-storage-connection-string>" \
  --public-access off

az containerapp secret set \
  --name <app-name> \
  --resource-group <resource-group> \
  --secrets enzo-render-storage='<storage-connection-string>'

az containerapp update \
  --name <app-name> \
  --resource-group <resource-group> \
  --set-env-vars \
    Enzo__RenderResults__Store=AzureBlob \
    Enzo__RenderResults__BlobContainerName=<container-name> \
    Enzo__RenderResults__BlobConnectionString=secretref:enzo-render-storage \
    Enzo__RenderResults__UrlLifetimeMinutes=30
```

SAS URL expiry prevents reads after the configured lifetime, but the blob object also needs cleanup. Configure a storage lifecycle rule for the hosted render prefix, for example deleting `render-results/` blobs after one day:

```json
{
  "rules": [
    {
      "enabled": true,
      "name": "delete-temporary-enzo-renders",
      "type": "Lifecycle",
      "definition": {
        "actions": {
          "baseBlob": {
            "delete": {
              "daysAfterModificationGreaterThan": 1
            }
          }
        },
        "filters": {
          "blobTypes": ["blockBlob"],
          "prefixMatch": ["<container-name>/render-results/"]
        }
      }
    }
  ]
}
```

For multiple-revision apps, the workflow assigns traffic to the latest revision. For single-revision apps, Azure makes the new revision live automatically.

Deployment verification checks that the Container App template references the SHA-tagged image and that provisioning succeeded. If the app has public ingress, the workflow also requests `/openapi/v1.json`, which does not require the `X-API-Key` header.

## Rollback

Images are immutable by commit SHA. To roll back, redeploy a previous known-good image:

```bash
az containerapp update \
  --name enzo-diagrams-api \
  --resource-group rg-enzo-diagrams \
  --image ghcr.io/solakpetri/enzo-diagrams-api:<previous-sha>
```

Use Azure Container Apps revision history or the Container App image value to identify which commit is currently deployed.
