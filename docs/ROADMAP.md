# MediaFlow Roadmap

## v0.1 — Safety prototype

- [x] .NET 10 solution and layered project structure
- [x] SQLite bootstrap and Share persistence
- [x] source/destination Shares
- [x] sync-tool-agnostic ignore patterns and presets
- [x] allowed-root validation and path probing
- [x] stable-file observation model
- [x] image/video extension discovery
- [x] ExifTool metadata extraction and capture-time preview
- [x] SHA-256 abstraction and implementation
- [ ] persist discovered media files
- [ ] Safe Move: copy → verify → commit → delete
- [ ] persisted operation state machine
- [ ] restart/recovery reconciliation
- [ ] safety and restart recovery tests

## v0.2 — Events and routing

- [ ] Events CRUD and persistence
- [ ] Source Groups
- [ ] capture-time matching
- [ ] late-sync routing
- [ ] destination templates
- [ ] conflict handling
- [ ] duplicate detection

## v0.3 — Web UI

- [ ] dashboard
- [x] Share configuration
- [x] path test and Dry-Run scan controls
- [x] metadata preview
- [ ] event management
- [ ] operation history
- [ ] quarantine
- [x] Dry Run default

## v0.4 — Integrations

- [x] REST endpoints foundation
- [ ] OpenAPI document/UI
- [ ] MQTT
- [x] Home Assistant examples
- [x] health endpoint

## v0.5 — Packaging

- [x] Dockerfile
- [x] ExifTool included in runtime image
- [x] Docker Compose example
- [ ] GHCR publishing
- [ ] backup/restore documentation
- [ ] onboarding wizard

## v1.0 — First stable release

- [ ] safety invariants fully tested
- [ ] migration/recovery behavior documented
- [ ] public security guidance
- [ ] contributor guide
- [ ] release notes / semantic versioning

## Later

- perceptual duplicate detection
- Live Photo awareness
- video fingerprints
- custom expression rules
- plugin metadata readers
- optional HACS integration
- notifications
- Prometheus metrics
- multi-user authorization
