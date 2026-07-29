# Azure learning environment

This implementation adds a real cloud architecture to the existing offline-first mobile project.

```text
MAUI mobile app -> App Service API -> Azure SQL Database
                         |
                         v
                  Service Bus queue -> Container Apps worker -> Azure SQL Database
```

## What each part demonstrates

| Azure service | Evidence in this repository |
| --- | --- |
| Azure App Service | `DocuRelay.Api` is a .NET 10 API with health checks, application settings and a managed identity. |
| Azure SQL Database | EF Core SQL Server provider, a checked-in initial migration, and document status persistence. |
| Microsoft Entra ID | `Microsoft.Identity.Web`, bearer-token validation and optional authorization applied to `/api/*`. |
| Azure Service Bus | API producer, versioned message contract, concurrent worker, retry by abandoning, dead-letter handling and an idempotency guard. |

## Safe local configuration

Never put credentials in `appsettings.json`. With an Azure SQL user/password connection string, set local secrets like this:

```bash
dotnet user-secrets set --project src/DocuRelay.Api "ConnectionStrings:AzureSql" "Server=tcp:<server>.database.windows.net,1433;Initial Catalog=docurelay;User ID=<user>;Password=<password>;Encrypt=True;TrustServerCertificate=False"
dotnet user-secrets set --project src/DocuRelay.Api "ServiceBus:FullyQualifiedNamespace" "<namespace>.servicebus.windows.net"
dotnet user-secrets set --project src/DocuRelay.Api "AzureAd:Instance" "https://login.microsoftonline.com/"
dotnet user-secrets set --project src/DocuRelay.Api "AzureAd:TenantId" "<tenant-id>"
dotnet user-secrets set --project src/DocuRelay.Api "AzureAd:ClientId" "<api-application-client-id>"
```

Start the API with `dotnet run --project src/DocuRelay.Api`. When `AzureAd:TenantId` is absent, authentication is intentionally disabled only for local exploration. In Azure, add all four `AzureAd__*` settings before exposing the API.

When `ConnectionStrings:AzureSql` is absent, the API uses a temporary EF Core in-memory database. This makes the project runnable without an Azure subscription; it is strictly a local fallback and is never used after an Azure SQL connection string is configured.

For the worker, set the equivalent `ConnectionStrings__AzureSql`, `ServiceBus__FullyQualifiedNamespace` and `ServiceBus__QueueName` environment variables. Azure hosts use `DefaultAzureCredential` and their managed identity, so no Service Bus key or SQL password is shipped with an application.

## Microsoft Entra setup

1. In **Microsoft Entra ID** → **App registrations**, create an API registration.
2. Under **Expose an API**, set an Application ID URI and add a delegated scope such as `access_as_user`.
3. In **Authentication**, add your mobile client redirect URI when you introduce interactive mobile sign-in.
4. Add the tenant ID and API client ID to the App Service configuration as `AzureAd__TenantId` and `AzureAd__ClientId`.
5. Request an access token whose audience is your API. A request without a valid bearer token is then rejected before it reaches an endpoint.

## SQL managed identities

After Bicep has deployed, set a Microsoft Entra administrator on the Azure SQL logical server. As that administrator, create a contained database user for each Bicep output (`apiPrincipalId`, `workerPrincipalId`) and grant least privilege. In a learning environment the API needs `db_datareader`, `db_datawriter` and temporarily `db_ddladmin` to apply its migration; the worker needs `db_datareader` and `db_datawriter`. Remove `db_ddladmin` after schema deployment, or run migrations in a separate deployment job.

## Failure behaviour you can demonstrate

- The API saves a document as `Pending` and its correlation ID in a transactional SQL outbox record. A dispatcher retries publishing the outgoing Service Bus message, so a queue outage cannot leave a saved document silently unprocessed.
- A worker moves it through `Processing` to `Completed`.
- If processing throws, the worker does **not** complete the Service Bus message. Service Bus redelivers it up to five times, then moves it to `document-processing/$DeadLetterQueue`.
- A repeated message for an already `Completed` document is completed without repeating the work. This is the idempotency proof.
- Query Application Insights and Container Apps logs by document ID, message ID or correlation ID when investigating a failure.

## Deploying

First provision through the Azure CLI using [`infra/main.bicep`](../infra/main.bicep), then finish the Entra ID and SQL identity bootstrap above. Alternatively configure GitHub environment secrets and variables listed in [the deployment workflow](../.github/workflows/azure-deploy.yml) and trigger it manually.

### Low-cost learning deployment

[`infra/learning.bicep`](../infra/learning.bicep) is the recommended first deployment. Its API runs on Linux F1, which is free but has only 60 CPU minutes per day and no Always On; expect a cold start after it has been idle. The worker remains a local process for this mode, but it still sends to/receives from the real Azure Service Bus and updates the real Azure SQL Database. This gives you valid hands-on experience without paying for a permanently running worker host.

The workflow uses GitHub OIDC; it does not require an Azure publish profile. Create an Azure federated credential for the GitHub repository/environment and give that deployment identity the narrowest practical access to the resource group plus `AcrPush` on the Container Registry. Supply `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID` and `AZURE_SQL_ADMIN_PASSWORD` as GitHub secrets. Supply `AZURE_RESOURCE_GROUP`, `AZURE_REGION` and `AZURE_NAME_PREFIX` as variables. `AZURE_NAME_PREFIX` must be globally unique and use only lowercase letters/numbers/hyphens.
