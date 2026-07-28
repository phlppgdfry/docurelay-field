# DocuRelay Field

> Offline-first field documentation for teams that cannot afford to lose evidence.

DocuRelay Field is a portfolio project demonstrating a realistic .NET MAUI workflow: a field worker opens a dossier, captures evidence, queues it while offline, and syncs it to an ASP.NET Core API when connectivity returns.

![Status](https://img.shields.io/badge/status-portfolio%20project-087E8B)
![.NET](https://img.shields.io/badge/.NET-10-512BD4)
![MAUI](https://img.shields.io/badge/.NET%20MAUI-iOS%20%7C%20Android-512BD4)

## Why this exists

This repository was built to demonstrate mobile engineering concerns that are easy to describe but harder to prove:

- REST API consumption with typed DTOs and `HttpClient`.
- A durable SQLite cache for dossiers and a persisted upload queue.
- Retryable document uploads that remain visible to the user when offline.
- Cross-platform .NET MAUI UI, testing, CI, and Android release packaging.

It is a standalone portfolio project. It has no relationship with an employer or customer.

## The workflow

```text
Field worker -> MAUI app -> local SQLite cache -> upload queue
                                 |                    |
                                 +-- offline ----------+
                                                      |
                                              connectivity restored
                                                      |
                                              ASP.NET Core API
```

1. Refresh dossiers from the API; the latest result is cached locally.
2. Add a document to a dossier. If the network is unavailable, the upload is queued.
3. Sync retries queued work in a controlled order and clearly reports failures.

## Tech stack

| Area | Choice |
| --- | --- |
| Mobile | .NET MAUI, MVVM, XAML, iOS and Android |
| Local persistence | SQLite via `sqlite-net-pcl` |
| Backend | ASP.NET Core Minimal API |
| Integration | REST, `HttpClient`, JSON, multipart upload |
| Quality | xUnit, GitHub Actions |
| Release | Android AAB artifact workflow, iOS release checklist |

## Quick start

### API

```bash
dotnet run --project src/DocuRelay.Api
```

The API starts with seeded dossiers and exposes a health endpoint at `/health`.

### Mobile app

```bash
dotnet workload restore
dotnet build src/DocuRelay.Mobile/DocuRelay.Mobile.csproj -f net10.0-android
```

Set `ApiOptions:BaseUrl` in `src/DocuRelay.Mobile/appsettings.json` to an API that is reachable from your device. An Android emulator typically needs `10.0.2.2` instead of `localhost`.

## Repository map

```text
src/
  DocuRelay.Api/        REST API with dossier and document endpoints
  DocuRelay.Contracts/  Shared API contracts
  DocuRelay.Mobile/     .NET MAUI client, cache and sync queue
tests/
  DocuRelay.Domain.Tests/  Fast, deterministic sync policy tests
docs/
  architecture.md       Design decisions and data flow
  offline-sync.md       Queue contract, retry semantics and failure handling
  release-checklist.md  Android and iOS release proof checklist
```

## Evidence for a technical reviewer

- Start at [`SyncService`](src/DocuRelay.Mobile/Services/SyncService.cs) for the offline upload policy.
- Read [`SqliteOfflineStore`](src/DocuRelay.Mobile/Services/SqliteOfflineStore.cs) for durable local state.
- Inspect [`Program.cs`](src/DocuRelay.Api/Program.cs) for the runnable REST API.
- CI validates the API and unit tests; tag builds produce an Android package artifact.

## Roadmap

- [x] Dossier retrieval and local cache
- [x] Persisted upload queue and retry policy
- [x] API document endpoint and seeded data
- [x] Automated API and sync-policy tests
- [ ] Physical Android device smoke test
- [ ] iOS TestFlight internal build
- [ ] Screenshot and short demo recording added to this README

## License

MIT. See [LICENSE](LICENSE).
