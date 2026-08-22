# Changelog

All notable changes to MediaFlow are documented here.

The project follows semantic versioning for tagged releases. Until v1.0, breaking changes may occur in minor releases when documented explicitly.

## Unreleased

## [1.0.2] - 2026-08-22

### Added

- long-running path checks, scans, metadata reads, routing previews, transfers, retries and image updates remain active during in-app navigation and report their result in a global background-task panel;

### Fixed

- the update UI now treats the updater-triggered connection drop as an expected restart, waits for the requested version and reloads automatically;
- persisted update status now reports the version of the running application immediately after restart instead of briefly showing the previous version.

## [1.0.1] - 2026-08-22

### Fixed

- bounded routing cycles now persistently rotate through large source shares instead of repeatedly evaluating the same first batch, preventing newer event media from starving;
- Synology `@eaDir` metadata thumbnails are excluded from media discovery regardless of the selected sync-tool preset.

## [1.0.0] - 2026-08-22

### Added

- .NET 10 layered application structure.
- SQLite persistence for Shares, Source Groups, Events, media index and operation state.
- transactional versioned SQLite schema migrations with `schema_migrations` history.
- legacy database baselining that preserves existing data.
- downgrade guard that refuses startup when the database schema is newer than the application supports.
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
- backup/restore, security, contributing, database migration and release documentation.
- Dependabot configuration for NuGet, Docker and GitHub Actions.
- quarantine review UI with audit-preserving manual dismissal.
- OpenAPI 3.1 document and Swagger UI at `/docs/`.
- configurable destination free-space reserve in runtime settings.
- CSV operation audit export and Prometheus-compatible `/metrics` endpoint.
- guided first-run onboarding.
- stable image update checks, changelog display, opt-in automatic updates and confirmed manual update triggering through an isolated Watchtower companion.
- release-tag version injection and persisted healthy-restart/update-failure reporting.
- deterministic Compose image pinning and an operator-triggered rollback runbook without adding another Docker-socket service.
- mounted-folder browser for Share paths, backed by `GET /api/v1/folders`.
- sidebar Web UI with one view per task, an overview of the running event, destination headroom and held files, a light theme and a phone layout.
- first-run setup wizard and a typed confirmation dialog for leaving Dry Run.

### Changed

- every SQLite connection now enables foreign-key enforcement and a 5-second busy timeout.

### Fixed

- routing preview now evaluates up to 2,000 files instead of stopping after the first 50 displayed by the console.
- share scan now counts every media file in the share instead of stopping at the sampled page, so large shares no longer report a flat `500 media files`.
- the console now shows the resolved destination folder (`/destinations/family/Sommerurlaub`) instead of the raw `{event.name}` template.
- the selected row in the mounted-folder browser no longer clips its folder name and path.
- the reported running version now keeps its prerelease suffix (`0.0.0-dev.42` instead of `0.0.0`), read from `InformationalVersion`.

### Security

- source deletion is guarded by persisted destination commit plus size/SHA-256 verification.
- ambiguous/recoverable states preserve the source.
- destination path resolution is restricted to the configured destination Share.
- Live mode is opt-in and requires explicit confirmation.
- older application builds refuse to mutate database schemas created by newer MediaFlow versions.
