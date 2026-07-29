# Runtime validation

## Android emulator smoke test — 28 July 2026

| Area | Evidence |
| --- | --- |
| Runtime | Pixel 3a Android 14 (API 34), arm64 emulator |
| Install | Release APK installed with `adb install` |
| Launch | `com.philippegodfroy.docurelayfield` became the focused activity |
| API flow | The MAUI client reached the local API through `10.0.2.2` and displayed both seeded dossiers |
| UI flow | Refresh and Sync queue were invoked; the screen remained responsive |
| Assets | [`android-runtime.png`](assets/android-runtime.png), [`android-runtime-demo.mp4`](assets/android-runtime-demo.mp4) and [`android-runtime-demo.gif`](assets/android-runtime-demo.gif) |

This is an emulator smoke test, not a substitute for testing on a physical Android handset. The capture predates the correction in `SqliteOfflineStore.GetOutstandingUploadsAsync`: enum values are now passed to sqlite-net as query parameters rather than translating `ToString()` into an unavailable SQLite function. Capture a new Android run before claiming post-fix runtime validation.

## iOS / TestFlight status

The codebase targets iOS, but an iOS device validation and TestFlight upload are intentionally not claimed yet:

- Installed .NET for iOS `26.5.10301` requires Xcode `26.6`; the local Xcode version is `26.3`.
- A paired iPhone 14 Pro Max is known to the Mac, but was not reachable over its current local-network connection.
- The keychain has Apple Development identities only. A TestFlight upload needs an Apple Distribution identity and an App Store Connect provisioning profile/account role.

Once those prerequisites are available, validate the same dossier, offline queue and upload-retry flow on the handset, archive the iOS Release build and upload it for an internal TestFlight group.
