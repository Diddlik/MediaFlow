# MomentFerry — Implementation Specification

## 1. Purpose

MomentFerry is a self-hosted service that safely routes media files between filesystem folders based on metadata, events and configurable rules.

MomentFerry does **not** implement synchronization itself. It works on filesystem folders that may already be synchronized by Resilio Sync, Syncthing, Nextcloud clients, FolderSync, SMB/NFS workflows, rsync, or any other mechanism.

The initial motivating scenario is a family vacation:

- each smartphone synchronizes its camera folder to a NAS;
- a common destination folder is synchronized back to all family devices;
- when a vacation/event is active, photos and videos captured during that event are routed into the common event folder;
- late-arriving files are still classified by their capture timestamp rather than their sync arrival time;
- source files are removed only after the destination copy has been safely verified.

The same engine should later support birthdays, weddings, trips, camera imports, video archives and other media-routing workflows.

---

## 2. Design principles

1. **Filesystem Shares are the integration boundary.** MomentFerry does not need sync-provider APIs.
2. **Safe Move is the default destructive operation.** Copy → Verify → Commit → Delete.
3. **Capture time determines event membership.** Arrival time is only a fallback signal.
4. **Unknown or ambiguous state always preserves the source.**
5. **All destructive operations are persisted and recoverable.**
6. **Filename collisions never overwrite different content by default.**
7. **Dry Run is a first-class feature.**
8. **The Web UI is the primary configuration interface.**
9. **Home Assistant and MQTT are optional integrations, not runtime dependencies.**
10. **Sync-tool presets are convenience defaults only.**

---

## 3. Core concepts

### 3.1 Share

A Share is a filesystem path visible inside the MomentFerry container.

Properties:

- `Id`
- `Name`
- `Path`
- `Role`: `Source`, `Destination`, or `Both`
- `Enabled`
- `Owner` optional
- `Group` optional
- `IgnorePatterns[]`
- `StabilitySeconds`
- `Recursive`
- `AllowedMediaTypes[]`
- `Preset` optional

Example:

```text
Name: Pavel Phone
Path: /sources/pavel
Role: Source
Owner: Pavel
Ignore:
  .sync/**
  *.!sync
  @eaDir/**
StabilitySeconds: 30
```

MomentFerry must not care which program synchronizes this path.

### 3.2 Share presets

Presets only pre-populate configuration. They do not add provider-specific behavior to the routing core.

Initial presets:

- Generic
- Resilio Sync
- Syncthing
- Synology NAS

Example Resilio defaults:

```text
.sync/**
*.!sync
```

All preset values remain editable.

### 3.3 Source Group

A Source Group combines multiple source Shares.

Example:

```text
Family
  - Pavel Phone
  - Elena Phone
  - Elisabeth Phone
```

The routing core therefore does not hard-code family members or smartphones.

### 3.4 Event

An Event represents a capture-time interval and destination context.

Properties:

- `Id`
- `Name`
- `Type` optional: Vacation, Birthday, Wedding, Custom
- `StartAt`
- `EndAt` nullable while active
- `Status`: Planned, Active, Closed, Archived, Cancelled
- `SourceGroupId`
- `DestinationShareId`
- `DestinationFolderTemplate`
- `OperationMode`
- `ConflictStrategy`
- `DuplicateStrategy`

Example:

```text
Name: Croatia 2026
Start: 2026-08-08T06:30:00+02:00
End: 2026-08-15T21:00:00+02:00
Sources: Family
Destination: Family Photos
Folder: {event.name}
Operation: SafeMove
```

### 3.5 Rule

V1 rules should support:

- source/share membership;
- image/video media type;
- capture timestamp inside event interval;
- extension allow/deny lists;
- optional minimum/maximum file size.

Future rules may use camera model, GPS, rating, filename regex, tags or custom expressions.

---

## 4. File discovery

Use a hybrid strategy:

1. filesystem watcher for fast detection;
2. periodic reconciliation scan for correctness.

Filesystem notifications are hints, not authoritative state. Reconciliation must recover from missed notifications, container restarts, remounts and large sync bursts.

### Ignore evaluation

Before processing, normalize each relative path and evaluate configured ignore patterns.

Typical patterns:

```text
.sync/**
*.!sync
@eaDir/**
#recycle/**
.thumbnails/**
```

Application-generated folders such as `.momentferry-staging` must always be excluded from source discovery.

### File stability

A discovered file must not be processed immediately.

Minimum stability criteria:

- file exists;
- path is not ignored;
- file can be opened for reading;
- size remains unchanged for `StabilitySeconds`;
- last-write timestamp remains unchanged for the same period;
- an optional second read/open check succeeds.

Suggested default: 30 seconds.

---

## 5. Metadata extraction

Use ExifTool as the initial metadata backend because it handles a broad range of image and video formats.

### Image timestamp priority

1. `DateTimeOriginal` + `OffsetTimeOriginal`
2. `CreateDate`
3. `ModifyDate`
4. filesystem timestamp fallback

### Video timestamp priority

1. `MediaCreateDate`
2. `CreateDate`
3. `TrackCreateDate`
4. filesystem timestamp fallback

### Timezone handling

Store internal timestamps as UTC while preserving the original offset where available.

If metadata has no timezone:

- use the Share's configured default timezone;
- mark the timezone as inferred;
- expose the inference in diagnostics.

Persist at least:

- source Share;
- source path;
- original filename;
- media type;
- extension;
- size;
- capture timestamp;
- timestamp source;
- timezone confidence;
- camera make/model when available;
- image dimensions;
- video duration;
- metadata extraction error when applicable.

---

## 6. Late synchronization

Routing is based primarily on `CapturedAt`, not discovery time.

Example:

```text
Event:          08 Aug 08:00 → 15 Aug 18:00
Photo captured: 09 Aug 14:53
Phone syncs:    16 Aug 11:00
```

The photo still belongs to the event.

Recommended behavior:

- every stable newly discovered media file is checked against active and recently closed events;
- bounded routing cycles prioritize unindexed files, then the least recently evaluated files, so large sources make persistent progress across restarts;
- platform metadata below Synology `@eaDir` directories is excluded from discovery;
- event matching uses capture timestamp;
- default event lookback: 90 days;
- archived events no longer receive automatic matches.

---

## 7. Operation modes

### Copy

- copy source to destination;
- verify destination;
- retain source.

### Safe Move — default

1. Create a durable database operation record.
2. Copy source to a staging path.
3. Flush and close the staged file.
4. Verify file length.
5. Compute SHA-256 for source and staged copy.
6. Require hashes to match.
7. Resolve final destination name.
8. Move staging file into the final destination when possible.
9. Verify final path exists.
10. Persist `DESTINATION_COMMITTED`.
11. Delete source.
12. Persist `COMPLETED`.

**The source must never be deleted before step 10.**

### Direct Move

Optional advanced mode only. It must not be the default.

### Archive

Copy and verify now; optionally delete the source after a configured retention interval.

---

## 8. Staging

Each destination Share should use a staging folder on the same destination filesystem where possible:

```text
<destination>/.momentferry-staging/
```

Staging filenames should use operation IDs rather than final user-visible names.

On startup MomentFerry must reconcile orphaned staging files with persisted operations.

---

## 9. Duplicate handling

Strong duplicate identity in v1:

```text
same size + same SHA-256
```

Filename is not part of duplicate identity.

Initial strategies:

- `KeepExisting`
- `KeepBoth`
- `SkipAndRecord`
- `SafeMoveToExisting`

`SafeMoveToExisting` is useful when the exact same media arrives from several synchronized devices: if an identical verified destination already exists, MomentFerry may record that destination and remove the duplicate source according to policy.

Future enhancements may include perceptual image hashes, video fingerprints and Live Photo pairing.

---

## 10. Filename conflicts

If the final destination filename already exists:

1. compare size and SHA-256;
2. if identical, apply duplicate strategy;
3. if different, apply conflict strategy.

Suggested default naming sequence:

```text
{originalName}
{originalStem}_{source}{extension}
{originalStem}_{source}_{counter:00}{extension}
```

Example:

```text
IMG_1234.jpg
IMG_1234_elena.jpg
IMG_1234_elena_02.jpg
```

Never overwrite a non-identical destination file by default.

---

## 11. Destination templates

Initial variables:

- `{event.name}`
- `{event.type}`
- `{year}`
- `{month}`
- `{day}`
- `{source}`
- `{owner}`

Examples:

```text
{event.name}
{year}/{event.name}
{year}/{month}/{event.name}
```

All generated paths must be sanitized and prevented from escaping the configured destination root.

---

## 12. Persisted operation state machine

```text
DISCOVERED
  ↓
WAITING_STABLE
  ↓
METADATA_PENDING
  ↓
METADATA_READY
  ↓
RULE_MATCHED
  ↓
COPY_PENDING
  ↓
COPYING
  ↓
VERIFYING
  ↓
DESTINATION_COMMITTED
  ↓
SOURCE_DELETE_PENDING
  ↓
COMPLETED
```

Error states:

```text
RETRY_PENDING
QUARANTINED
IGNORED
FAILED
```

Every transition must be persisted. No destructive filesystem action may depend solely on in-memory state.

---

## 13. Database

SQLite is sufficient for v1.

Suggested tables:

### `shares`

- id
- name
- path
- role
- owner
- enabled
- preset
- stability_seconds
- recursive
- created_at
- updated_at

### `share_ignore_patterns`

- id
- share_id
- pattern
- sort_order

### `source_groups`

- id
- name

### `source_group_members`

- group_id
- share_id

### `events`

- id
- name
- type
- start_at_utc
- end_at_utc
- timezone
- status
- source_group_id
- destination_share_id
- destination_template
- operation_mode
- conflict_strategy
- duplicate_strategy

### `media_files`

- id
- source_share_id
- source_path
- original_name
- size
- extension
- media_type
- captured_at_utc
- captured_offset
- timestamp_source
- sha256 nullable until required
- first_seen_at
- last_seen_at
- metadata_json optional

### `operations`

- id
- media_file_id
- event_id nullable
- state
- source_path
- staging_path
- destination_path
- source_hash
- destination_hash
- retry_count
- last_error
- started_at
- completed_at

### `audit_log`

- id
- timestamp
- severity
- category
- action
- entity_type
- entity_id
- message
- details_json

---

## 14. Idempotency and restart recovery

On startup:

1. open database and apply migrations;
2. reconcile incomplete operations;
3. validate configured Shares;
4. scan staging directories;
5. resume safe operations from the last persisted state;
6. start filesystem watchers;
7. start periodic reconciliation.

Examples:

- `COPYING` + staging exists → verify or recopy;
- `VERIFYING` → recompute hash;
- `DESTINATION_COMMITTED` + source exists → perform source deletion;
- `SOURCE_DELETE_PENDING` + source missing → mark completed;
- ambiguous filesystem/database mismatch → quarantine instead of guessing.

---

## 15. Quarantine / Needs Attention

Any ambiguous or unsafe state retains the source.

Examples:

- metadata cannot be parsed;
- hash mismatch;
- destination inaccessible;
- disk full;
- destination conflict cannot be resolved;
- source changed during copy;
- database state conflicts with filesystem state.

UI actions:

- Retry
- Ignore
- Copy only
- Mark resolved
- Open details

Never auto-delete quarantined sources.

---

## 16. Dry Run

Dry Run is mandatory for v1.

When enabled MomentFerry should:

- discover files;
- read metadata;
- evaluate events/rules;
- resolve destination path and filename conflicts;
- show the operation that would happen;
- perform no copy, move or delete.

Example:

```text
Would move
/sources/pavel/IMG_1203.jpg
→ /destinations/family/Croatia 2026/IMG_1203.jpg
```

---

## 17. Web UI

### Dashboard

Display:

- active events;
- source/destination health;
- files processed today;
- pending operations;
- quarantined files;
- recent activity;
- disk-space warnings;
- current Dry Run state.

### Shares

CRUD interface for:

- name;
- path;
- role;
- owner;
- recursive mode;
- media types;
- ignore patterns;
- stability duration;
- optional preset.

Path browsing must only expose allowed/mounted container roots.

Provide a test action for:

- path existence;
- read/write access;
- free space where available.

### Source Groups

Create reusable collections of source Shares.

### Events

Functions:

- create planned event;
- start now;
- stop now;
- set fixed interval;
- reopen/edit closed event;
- archive event;
- inspect routed files.

### Operations

Search/filter by source, event, state, date, filename and error.

### Settings

- timezone;
- reconciliation interval;
- global Dry Run;
- hash settings;
- MQTT configuration;
- authentication;
- log level.

---

## 18. API

Version HTTP APIs under `/api/v1`.

Initial endpoints:

```text
GET    /api/v1/health
GET    /api/v1/shares
POST   /api/v1/shares
PUT    /api/v1/shares/{id}
DELETE /api/v1/shares/{id}

GET    /api/v1/events
POST   /api/v1/events
POST   /api/v1/events/{id}/start
POST   /api/v1/events/{id}/stop
POST   /api/v1/events/{id}/archive

GET    /api/v1/operations
GET    /api/v1/operations/{id}
POST   /api/v1/operations/{id}/retry

POST   /api/v1/scan
GET    /api/v1/status
```

OpenAPI/Swagger should be enabled.

---

## 19. MQTT and Home Assistant

MQTT is optional.

Suggested topics:

```text
momentferry/events/command
momentferry/events/state
momentferry/status
momentferry/activity
```

Example command:

```json
{
  "command": "start",
  "name": "Croatia 2026",
  "timestamp": "2026-08-08T06:30:00+02:00"
}
```

Home Assistant should initially integrate through REST or MQTT. A HACS integration may be added later.

Suggested HA concepts:

- vacation/event mode toggle;
- event name;
- start/stop actions;
- photo/video counters;
- MomentFerry health sensor.

---

## 20. Security

Filesystem requirements:

- never browse arbitrary host paths outside configured mounted roots;
- normalize and validate all paths;
- reject path traversal;
- destination writes remain inside the destination Share;
- source deletion targets only the exact previously discovered source path.

Web requirements:

- support local authentication or clearly document reverse-proxy authentication;
- do not recommend unauthenticated public exposure;
- never log MQTT or future external credentials.

---

## 21. Observability

Use structured logging with Serilog.

Suggested categories:

- Discovery
- Metadata
- Routing
- Copy
- Verification
- Delete
- Event
- API
- Recovery
- Quarantine

Future metrics:

- processed files total;
- bytes copied;
- failures;
- pending operations;
- hash duration;
- metadata extraction duration;
- scan duration.

---

## 22. Docker deployment

Target a single-container v1 where practical.

```yaml
services:
  momentferry:
    image: ghcr.io/OWNER/momentferry:latest
    container_name: momentferry
    restart: unless-stopped
    ports:
      - "8080:8080"
    environment:
      TZ: Europe/Berlin
    volumes:
      - ./data:/app/data
      - /volume1/photos/pavel:/sources/pavel
      - /volume1/photos/elena:/sources/elena
      - /volume1/photos/elisabeth:/sources/elisabeth
      - /volume1/photos/family:/destinations/family
```

The application should enumerate only mounted/allowed roots rather than the host filesystem.

---

## 23. Suggested solution structure

```text
MomentFerry/
├── src/
│   ├── MomentFerry.Core/
│   ├── MomentFerry.Application/
│   ├── MomentFerry.Infrastructure/
│   ├── MomentFerry.Metadata.ExifTool/
│   ├── MomentFerry.Web/
│   └── MomentFerry.Worker/
├── tests/
│   ├── MomentFerry.Core.Tests/
│   ├── MomentFerry.Application.Tests/
│   ├── MomentFerry.Infrastructure.Tests/
│   └── MomentFerry.IntegrationTests/
├── docs/
├── examples/
├── Dockerfile
├── docker-compose.yml
└── MomentFerry.sln
```

Avoid sync-provider abstractions in v1. The filesystem Share is the boundary.

---

## 24. Suggested domain interfaces

```csharp
public interface IFileDiscoveryService
{
    IAsyncEnumerable<DiscoveredFile> DiscoverAsync(
        Share source,
        CancellationToken cancellationToken);
}

public interface IFileStabilityService
{
    Task<FileStabilityResult> WaitUntilStableAsync(
        string path,
        TimeSpan stableFor,
        CancellationToken cancellationToken);
}

public interface IMediaMetadataReader
{
    Task<MediaMetadata> ReadAsync(
        string path,
        CancellationToken cancellationToken);
}

public interface IFileHashService
{
    Task<string> ComputeSha256Async(
        string path,
        CancellationToken cancellationToken);
}

public interface IRoutingEngine
{
    Task<RouteDecision> EvaluateAsync(
        MediaFile file,
        CancellationToken cancellationToken);
}

public interface ISafeFileOperationService
{
    Task<OperationResult> ExecuteAsync(
        FileOperation operation,
        CancellationToken cancellationToken);
}
```

---

## 25. Concurrency and large files

Requirements:

- configurable worker concurrency;
- lock/lease per source path;
- never process the same source concurrently;
- destination conflict resolution must be atomic under an application-level lock;
- use short SQLite transactions;
- copy and hash outside long DB transactions;
- stream file copies and SHA-256 calculations;
- never load an entire video into memory;
- support cancellation and progress for large files;
- optionally enforce a free-space reserve before Safe Move.

---

## 26. Test strategy

### Unit tests

- ignore pattern matching;
- timestamp priority;
- timezone fallback;
- destination template rendering;
- conflict naming;
- duplicate decisions;
- rule matching;
- state transitions.

### Integration tests

Simulate:

- files appearing progressively;
- source changes during copy;
- duplicate names with different content;
- identical content with different names;
- process restart between state transitions;
- source deletion failure;
- late synchronized files;
- ignored Resilio/Synology artifacts;
- unavailable destination;
- interrupted copy.

### Critical invariant

Tests must prove:

> No code path can delete a source before a verified, committed destination exists when using Safe Move.

---

## 27. Initial acceptance criteria

V1 is acceptable when a user can:

1. run MomentFerry with Docker Compose;
2. open the Web UI;
3. configure multiple source Shares and one destination Share;
4. configure ignore patterns and stability time;
5. create a Source Group;
6. create/start an Event;
7. add test photos/videos to source Shares;
8. route them based on capture metadata;
9. Safe Move them into the event folder;
10. process late-arriving media after the event ends;
11. survive a forced container restart without losing media;
12. resolve filename collisions without overwrite;
13. detect byte-identical duplicates;
14. inspect operations in an audit log;
15. run the same workflow in Dry Run mode;
16. control event start/stop through REST and MQTT.

---

## 28. Implementation order

### Phase 1 — Core safety

- solution/project skeleton;
- SQLite/EF Core schema;
- Share model;
- discovery/reconciliation;
- ignore patterns;
- stability detection;
- ExifTool metadata;
- SHA-256;
- Safe Move state machine;
- restart recovery;
- safety tests.

### Phase 2 — Routing

- Events;
- Source Groups;
- destination templates;
- duplicate handling;
- conflict handling;
- late-sync handling.

### Phase 3 — UI/API

- Blazor UI;
- Share setup;
- Event setup/control;
- operations/audit views;
- Dry Run;
- REST API + OpenAPI.

### Phase 4 — Integrations

- MQTT;
- Home Assistant examples;
- health endpoints;
- Docker image;
- GHCR publishing.

### Phase 5 — Public release readiness

- onboarding wizard;
- documentation/screenshots;
- backup/recovery guidance;
- security documentation;
- contributor guide;
- release workflow;
- semantic versioning.
- quarantine management, audit export and operational metrics;
- opt-in image updates through a narrowly scoped companion service;
- update changelog, confirmation and result display in the Web UI.
