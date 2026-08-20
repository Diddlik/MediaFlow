# Changelog

All notable changes to MediaFlow are documented here.

The project follows semantic versioning for tagged releases. Until v1.0, breaking changes may occur in minor releases when documented explicitly.

## Unreleased

### Added

- .NET 10 layered application structure.
- SQLite persistence for Shares, Source Groups, Events, media index and operation state.
- sync-tool-agnostic filesystem Share model and presets.
- ExifTool image/video metadata extraction with timezone-aware capture timestamps.
- capture-time Event matching including late synchronization after an Event is closed.
- routing preview and persistent media indexing.
- Safe Move (`copy → verify → commit → delete`) and Copy operation modes.
- SHA-256 duplicate verification and configurable filename conflict handling.
- persistent operation state machine, restart recovery and explicit retry.
- Dry Run default and explicit Live-mode confirmation.
- Web UI for Shares, Source Groups, Events, routing preview, operations and runtime safety settings.
- periodic reconciliation worker plus FileSystemWatcher wake-ups.
- per-media transfer serialization to avoid concurrent duplicate execution.
- automated Safe Move tests covering verification, persistence failures and source-delete failures.
- destination free-space detection and 512 MiB reserve before real staging copies.
- `/api/v1/storage` destination capacity status.
- optional MQTT event control and Home Assistant REST/MQTT examples.
- Docker image with ExifTool and healthcheck.
- CI Release build, automated tests and Docker build validation.
- GHCR publishing with `latest`, commit SHA and SemVer tags.
- backup/restore, security, contributing and release documentation.
- Dependabot configuration for NuGet, Docker and GitHub Actions.

### Security

- source deletion is guarded by persisted destination commit plus size/SHA-256 verification.
- ambiguous/recoverable states preserve the source.
- destination path resolution is restricted to the configured destination Share.
- Live mode is opt-in and requires explicit confirmation.

## 0.x history

The repository is still in active pre-release development. The first formal tagged release will create the first dated/versioned changelog section from the `Unreleased` entries above.
