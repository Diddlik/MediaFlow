# MediaFlow

MediaFlow is a self-hosted media routing and collection service for synchronized folders.

It watches one or more source folders, identifies media using metadata and configurable rules, and safely routes files into shared destination folders. MediaFlow is deliberately sync-tool agnostic: Resilio Sync, Syncthing, Nextcloud clients, FolderSync, SMB uploaders, rsync, or any other software may synchronize the folders. MediaFlow only requires access to the resulting filesystem paths.

## Primary use case

A family has multiple smartphones. Each phone synchronizes its camera folder to a NAS. When a vacation/event is active, MediaFlow collects photos and videos captured during the event into a shared destination folder. Existing sync software then distributes that shared folder back to all phones.

The default destructive operation is **Safe Move**:

1. Detect a stable source file.
2. Read capture metadata.
3. Match an active or historical event/rule.
4. Copy to staging/destination.
5. Verify size and SHA-256.
6. Commit the operation to the database.
7. Delete the source only after successful verification.

This makes the operation appear as a move to synchronized clients while minimizing the risk of data loss.

## Core principles

- Sync-tool agnostic: folders/shares are the integration boundary.
- Safe by default: never delete before verified copy.
- Metadata-aware: use EXIF/video capture time rather than sync arrival time.
- Late-sync aware: files arriving after an event ends can still be routed by capture timestamp.
- Idempotent: restarts must not duplicate or lose files.
- Configurable conflict and duplicate handling.
- Web UI first; configuration files remain optional.
- Docker-first deployment.
- Optional Home Assistant, MQTT, REST and webhook control.

## Planned stack

- .NET 10
- ASP.NET Core
- Blazor Web App
- EF Core + SQLite
- Background worker
- ExifTool for metadata extraction
- Docker / Docker Compose
- Serilog structured logging

## Documentation

- [Implementation specification](docs/IMPLEMENTATION.md)
- [Architecture](docs/ARCHITECTURE.md)
- [Roadmap](docs/ROADMAP.md)

## Status

Design / specification phase.
