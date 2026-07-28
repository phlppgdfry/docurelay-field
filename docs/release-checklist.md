# Release checklist

This checklist is deliberately committed to make release ownership inspectable.

## Before every release

- [ ] API tests pass and the Android MAUI build is green in CI.
- [ ] Version and build number are increased.
- [ ] Device smoke test covers refresh, offline file queue, reconnect and sync.
- [ ] API base URL uses HTTPS and points to the intended environment.
- [ ] App icon, screenshots, support URL and privacy information are current.
- [ ] No development credentials, API keys or local databases are committed.

## Android internal testing

- [ ] Generate a signed AAB from the release workflow.
- [ ] Upload to Google Play internal testing.
- [ ] Verify installation, permissions and upload flow on a physical device.
- [ ] Confirm crash reporting and support contact details.

## iOS TestFlight

- [ ] Configure signing, bundle identifier and capabilities in Apple Developer.
- [ ] Archive with the iOS target on a Mac.
- [ ] Upload to App Store Connect and complete TestFlight metadata.
- [ ] Test on a physical iPhone, including camera/file picker permissions.
- [ ] Review privacy labels and export compliance before submission.

