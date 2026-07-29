# Portfolio walkthrough

Use this five-minute walkthrough during a technical interview or demo.

## 1. Start locally, without cost

```bash
dotnet run --project src/DocuRelay.Api --urls http://127.0.0.1:5086
```

Open `http://127.0.0.1:5086/demo`. This page is not mocked: it reads the same health and dossier endpoints as the MAUI app.

## 2. Show the field workflow

1. Open DocuRelay Field in the Android emulator and select a dossier.
2. Choose **Add document**. The file is copied to the device cache and recorded in SQLite before any network call is attempted.
3. The three queue cards make waiting, retrying and terminal failures visible. Turn the network off to demonstrate that the upload remains available.
4. Restore the network and select **Sync queue**. For failed local uploads, **Retry failed** resets the local retry state and immediately attempts processing again.

## 3. Show the server workflow

1. After upload, open the selected dossier. Its document list shows the server’s `Pending`, `Processing`, `Completed` or `Failed` state.
2. Explain that the API writes both document metadata and an outbox message in one EF Core transaction.
3. The local demo dispatcher uses an in-process channel; production configuration changes only the adapter to Azure Service Bus.
4. The worker has an idempotency guard: a second delivery of a completed document does not execute the work twice.

## 4. Show the engineering proof

```bash
dotnet test tests/DocuRelay.Domain.Tests --configuration Release
dotnet test tests/DocuRelay.Api.IntegrationTests --configuration Release
```

The integration test performs an actual multipart upload and waits until the local outbox and worker return `Completed`. It is the strongest no-cost proof that the asynchronous design works.

## 5. Map it to Azure

Use the [architecture diagram](assets/azure-ready-processing-flow.svg) and explain the swap:

| Local demonstration | Azure deployment |
| --- | --- |
| EF Core InMemory | Azure SQL Database |
| In-process channel | Azure Service Bus queue |
| Local worker | .NET Worker on Azure Container Apps |
| No authentication | Microsoft Entra ID bearer tokens |
| `App_Data` file adapter | Azure Blob Storage adapter (future extension) |

The repository already contains Bicep and GitHub OIDC deployment workflows. Azure deployment is deliberately not claimed until the subscription can create the needed resources.
