# MediaFlow Roadmap

## v0.1 — Safety prototype

- [x] .NET 10 solution and layered project structure
- [x] SQLite bootstrap and Share persistence
- [x] source/destination Shares
- [x] sync-tool-agnostic ignore patterns and presets
- [x] allowed-root validation and path probing
- [x] stable-file observation model
- [x] image/video extension discovery
- [x] ExifTool metadata extraction
- [x] SHA-256 implementation
- [x] persist discovered media files
- [x] Safe Move: copy → verify → commit → delete
- [x] persisted operation state machine
- [x] restart/recovery reconciliation
- [x] safety invariant and failure-path tests
- [x] per-media transfer serialization

## v0.2 — Events and routing

- [x] Events CRUD and persistence
- [x] Source Groups
- [x] capture-time matching
- [x] late-sync routing for closed events
- [x] destination templates
- [x] conflict handling
- [x] SHA-256 duplicate detection
- [x] Copy and Safe Move execution
- [x] explicit retry of recoverable operations

## v0.3 — Web UI

- [x] automation/safety dashboard
- [x] Share configuration
- [x] path test and Dry-Run scan controls
- [x] metadata preview
- [x] event management and start/stop
- [x] routing preview
- [x] operation history
- [x] runtime Dry Run / Live settings
- [x] worker status
- [x] destination storage status
- [ ] dedicated quarantine management view

## v0.4 — Integrations

- [x] REST API
- [ ] OpenAPI document/UI
- [x] optional MQTT event control
- [x] Home Assistant REST examples
- [x] Home Assistant MQTT examples
- [x] health endpoint

## v0.5 — Automation and deployment

- [x] periodic reconciliation worker
- [x] FileSystemWatcher wake-ups with reconciliation fallback
- [x] stability-delay wake-up for newly synchronized files
- [x] persistent runtime settings
- [x] Dockerfile
- [x] ExifTool included in runtime image
- [x] Docker Compose example
- [x] container healthcheck
- [x] GitHub Actions build + tests + Docker validation
- [x] GHCR publishing
- [x] destination free-space guard with reserve
- [ ] onboarding wizard

## v0.6 — Operations and maintainability

- [x] backup/restore documentation
- [x] public security guidance
- [x] contributor guide
- [x] automated dependency update configuration
- [ ] configurable destination free-space reserve
- [ ] structured audit export
- [ ] dedicated metrics endpoint / Prometheus

## v1.0 — First stable release

- [x] critical source-deletion invariant covered by automated tests
- [x] restart recovery behavior implemented
- [ ] database migration/versioning strategy finalized
- [ ] quarantine workflow finalized
- [ ] OpenAPI finalized
- [ ] release notes / semantic versioning policy
- [ ] first tagged stable release

## Later

- perceptual duplicate detection
- Live Photo awareness
- video fingerprints
- custom expression rules
- plugin metadata readers
- optional HACS integration
- notifications
- Prometheus/Grafana dashboards
- multi-user authorization
