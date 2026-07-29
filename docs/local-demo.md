# Local end-to-end demo

You can demonstrate the complete document workflow without an Azure subscription or paid resources.

```bash
dotnet run --project src/DocuRelay.Api --urls http://127.0.0.1:5086
```

Open these URLs while the API runs:

- `http://127.0.0.1:5086/health` — service health.
- `http://127.0.0.1:5086/openapi/v1.json` — OpenAPI description.
- `http://127.0.0.1:5086/api/dossiers` — seeded field dossiers.
- `http://127.0.0.1:5086/demo` — visual processing console with live API, dossier and worker status.

## What the demo proves

1. Upload a document from the MAUI app or through the multipart API endpoint.
2. The API validates the upload, stores its content under `src/DocuRelay.Api/App_Data/documents/`, and creates the document plus outbox message together.
3. The local outbox dispatcher publishes the message to an in-process queue.
4. The local worker moves the document from `Pending` through `Processing` to `Completed`.
5. Select the dossier in the mobile app to view file-level `Pending`, `Processing`, `Completed` or `Failed` status.
6. The mobile queue dashboard shows waiting, retrying and failed uploads; **Retry failed** resets terminal local failures and immediately attempts the queue again.
7. Download the original file with `GET /api/dossiers/{dossierId}/documents/{documentId}/content`.

`App_Data` is ignored by Git and is intentionally disposable. When Azure configuration is supplied, the API switches to Azure SQL and Azure Service Bus automatically. The separate `DocuRelay.Worker` project is the cloud-hosted Service Bus consumer; the in-process worker exists only to make the no-cost local demonstration complete.
