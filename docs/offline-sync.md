# Offline sync contract

## Upload states

| State | Meaning | Next action |
| --- | --- | --- |
| `Pending` | File was captured and durable local state was written. | Attempt upload. |
| `Retrying` | A transient request failed. | Retry in a later sync attempt. |
| `Synced` | Server confirmed the upload. | Remove local file. |
| `Failed` | Retry limit reached or local file is unavailable. | Keep record and surface recovery action. |

## Invariants

1. A selected file is copied locally before the queue is written.
2. A queue item remains until the server confirms it.
3. Each upload has a stable local identifier, target dossier and file metadata.
4. A failure is retained with context instead of being swallowed.

## Deliberate scope

The current API demo is in-memory, so it models the request/response flow but not cloud durability. Production hardening would add an idempotency key to each upload, back-off scheduling, background sync, conflict resolution, and a user-triggered retry for terminal failures.

