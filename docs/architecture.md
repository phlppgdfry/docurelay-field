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
  Minimal API endpoints -> DossierStore -> seeded, in-memory demo data
```

The API is intentionally small so a reviewer can run the entire workflow locally. In production, `DossierStore` would be replaced by an authenticated persistence layer and object storage, without changing the mobile API client contract.

## Decisions

### Shared contracts, not shared business logic

`DocuRelay.Contracts` keeps REST DTOs and small deterministic policy code in one place. The mobile app still owns offline persistence; the API still owns server state.

### Persist before sync

When a user selects a file, it is copied into the app cache and a queue record is committed to SQLite before any network request. A failed process or loss of connectivity cannot silently discard the intent to upload.

### Bounded retries with a visible terminal state

Transient network errors move an item from `Pending` to `Retrying`. After three failed attempts it becomes `Failed`, retaining the last error for a user-facing retry flow. The policy is unit-tested.

### No optimistic claim of success

An item is marked `Synced` only after the API returns a successful HTTP status. Local file cleanup happens after that state transition.

## Security notes for production

- Add OAuth/OIDC tokens and a per-dossier authorization policy.
- Store queued files in an OS-protected location and encrypt sensitive document metadata at rest.
- Replace HTTP development endpoints with HTTPS only.
- Apply server-side file size, content-type and malware scanning controls.

