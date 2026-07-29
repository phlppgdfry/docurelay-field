# Architecture

## Goal

DocuRelay Field prioritizes one real mobile failure mode: a user must be able to capture evidence without a reliable network and must never be left wondering whether the evidence arrived.

## Boundaries

```text
DocuRelay.Mobile
  Views + ViewModels
      |
  DossierRepository ---- DossierApiClient ---- REST API
      |
  SqliteOfflineStore
      |
  SQLite cache + persisted upload queue

DocuRelay.Api
  Minimal API endpoints -> EF Core -> Azure SQL Database
                         |
                         +-> Azure Service Bus: document-processing
                                      |
                                      v
                         DocuRelay.Worker (Azure Container Apps)
                                      |
                                      +-> Azure SQL status updates
```

The API remains deliberately small, but now uses an EF Core SQL Server context and checked-in migration. The mobile API contract stays unchanged. Azure SQL, Service Bus and Entra ID settings are supplied by user secrets locally or managed App Service/Container Apps configuration in Azure.

Without an Azure SQL connection string, the API switches to EF Core's in-memory provider. Without a Service Bus namespace, the outbox uses an in-process channel and a local worker. These fallbacks exist solely for the no-cost local demo; the cloud configuration uses the Azure implementations.

## Decisions

### Shared contracts, not shared business logic

`DocuRelay.Contracts` keeps REST DTOs and small deterministic policy code in one place. The mobile app still owns offline persistence; the API still owns server state.

### Persist before sync

When a user selects a file, it is copied into the app cache and a queue record is committed to SQLite before any network request. A failed process or loss of connectivity cannot silently discard the intent to upload.

### Bounded retries with a visible terminal state

Transient network errors move an item from `Pending` to `Retrying`. After three failed attempts it becomes `Failed`, retaining the last error for a user-facing retry flow. The policy is unit-tested.

### No optimistic claim of success

An item is marked `Synced` only after the API returns a successful HTTP status. Local file cleanup happens after that state transition.

### Asynchronous server processing

The API persists a new document as `Pending` and its versioned `DocumentProcessingMessage` in the same SQL transaction. An outbox dispatcher safely retries publishing the message. The worker explicitly completes a Service Bus message only after the status is `Completed`. An exception is left to Service Bus for retry and, after five delivery attempts, dead-lettering. Because Service Bus is at-least-once, the worker treats an already completed document as a harmless duplicate.

### File storage boundary

The local demo stores raw upload content in the ignored `App_Data/documents` directory and exposes a scoped download endpoint. The database stores metadata and a storage path, not raw content. An Azure Blob Storage implementation can replace this local adapter when cloud storage becomes available.

### Identity without secret connection strings

The deployed API and worker use system-assigned managed identities through `DefaultAzureCredential`. Bicep grants only the Service Bus sender/receiver roles. SQL permissions and the Microsoft Entra API registration are deliberate tenant-owner setup steps documented in [the Azure learning guide](azure-learning-guide.md).

## Security notes for production

- Microsoft Entra bearer-token validation is enabled when `AzureAd:TenantId` is configured; add per-dossier authorization policies before handling real customer data.
- Store queued files in an OS-protected location and encrypt sensitive document metadata at rest.
- Replace HTTP development endpoints with HTTPS only.
- Apply server-side file size, content-type and malware scanning controls.
