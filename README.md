# MediaFlow

MediaFlow is a self-hosted media routing and collection service for synchronized folders.

It watches filesystem shares, identifies photos and videos from metadata, matches them to capture-time events, and safely routes them into shared destination folders. MediaFlow is deliberately sync-tool agnostic: Resilio Sync, Syncthing, Nextcloud clients, FolderSync, SMB uploaders, rsync, or another tool may synchronize the folders. MediaFlow only works with the resulting filesystem paths.

## Primary use case

Several phones synchronize their camera folders to a NAS. During a vacation or another event, MediaFlow collects media captured inside the event window into a common destination folder. Existing sync software can then distribute that shared folder back to all participating phones.

Late synchronization is supported: a photo arriving after an event has ended is still matched using its capture timestamp.

## Safety model

The default destructive operation is **Safe Move**:

1. Wait until the source file is stable.
2. Read capture metadata with ExifTool.
3. Match exactly one event.
4. Persist the media file and operation state.
5. Copy into a destination-side staging directory.
6. Verify size and SHA-256.
7. Commit the verified file to its final destination.
8. Persist `DestinationCommitted`.
9. Delete the source only after the committed destination is verified.

If MediaFlow cannot prove the destination is safe, the source is preserved. Incomplete operations are reconciled after restart.

**Dry Run is enabled by default.** Live transfers require an explicit confirmation in the Web UI.

## Implemented

- .NET 10 / ASP.NET Core
- SQLite persistence
- Web UI
- source and destination Shares
- Source Groups
- capture-time Events with start/stop
- image and video discovery
- stable-file detection
- ExifTool metadata extraction
- timezone-aware capture timestamps
- late-sync matching for closed events
- routing preview
- SHA-256 duplicate verification
- filename conflict handling
- Safe Move and Copy
- persistent operation state machine
- restart recovery and explicit retry
- background reconciliation worker
- persistent runtime settings
- Dry Run / Live mode safety gate
- automation status endpoint
- REST API
- Home Assistant REST examples
- Docker / Docker Compose
- GitHub Actions build, tests and container validation
- GHCR publishing workflow

## Quick start

Copy `docker-compose.example.yml` and adapt the NAS paths:

```yaml
services:
  mediaflow:
    image: ghcr.io/diddlik/mediaflow:latest
    restart: unless-stopped
    ports:
      - "8080:8080"
    volumes:
      - ./data:/app/data
      - /path/to/phone1:/sources/phone1
      - /path/to/phone2:/sources/phone2
      - /path/to/shared:/destinations/family
```

Then start it:

```bash
docker compose up -d
```

Open `http://<server>:8080`.

The `data` volume contains the SQLite database and persistent runtime settings. Docker environment values act as initial defaults; settings saved in the Web UI are stored in `data/runtime-settings.json` and take precedence for runtime automation values.

## Recommended first setup

1. Keep **Dry Run** enabled.
2. Add each synchronized camera folder as a Source Share.
3. Add the common family folder as a Destination Share.
4. Create a Source Group containing the phone shares.
5. Create and start an Event.
6. Use Routing Preview and verify capture times and destination paths.
7. Only after testing, explicitly enable Live mode if Safe Move/Copy should run automatically.

## REST endpoints

Important endpoints include:

```text
GET  /health
GET  /api/v1/info
GET  /api/v1/status
GET  /api/v1/settings
PUT  /api/v1/settings
GET  /api/v1/shares
GET  /api/v1/events/
POST /api/v1/events/{id}/start
POST /api/v1/events/{id}/stop
GET  /api/v1/shares/{id}/routing-preview
GET  /api/v1/operations
POST /api/v1/recovery
```

## Development

```bash
dotnet restore MediaFlow.sln
dotnet build MediaFlow.sln -c Release
dotnet test MediaFlow.sln -c Release
```

The Docker image additionally contains ExifTool and a container healthcheck.

## Documentation

- [Implementation specification](docs/IMPLEMENTATION.md)
- [Architecture](docs/ARCHITECTURE.md)
- [Roadmap](docs/ROADMAP.md)
- [Home Assistant example](examples/home-assistant/README.md)

## Status

Active development. Core routing, safety, recovery, background automation and Docker deployment are implemented; additional integrations and production hardening are ongoing.
