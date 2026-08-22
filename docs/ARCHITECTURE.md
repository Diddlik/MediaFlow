# MomentFerry Architecture

## System context

```text
 Phone A ──sync software──> Source Share A ─┐
 Phone B ──sync software──> Source Share B ─┼─> MomentFerry ─> Shared Destination Share
 Phone C ──sync software──> Source Share C ─┘                    │
                                                                └─sync software─> Phones
```

The sync software is outside the MomentFerry boundary.

## Logical components

```text
┌────────────────────────────────────────────────────┐
│                    MomentFerry                       │
│                                                    │
│  Web UI / REST / MQTT                              │
│          │                                         │
│          ▼                                         │
│  Event & Configuration Services                    │
│          │                                         │
│          ▼                                         │
│  Discovery ─> Stability ─> Metadata ─> Routing    │
│                                      │             │
│                                      ▼             │
│                              Safe File Engine      │
│                                      │             │
│                              Copy / Hash / Delete  │
│                                      │             │
│                              SQLite + Audit        │
└────────────────────────────────────────────────────┘
```

## Dependency direction

```text
MomentFerry.Core
      ↑
MomentFerry.Application
      ↑
MomentFerry.Infrastructure
      ↑
MomentFerry.Web / MomentFerry.Worker
```

Core must not depend on EF Core, ExifTool, MQTT or ASP.NET.

## Filesystem boundary

The filesystem path is the primary integration contract.

MomentFerry should not need to know whether a Share is synchronized with:

- Resilio Sync;
- Syncthing;
- Nextcloud;
- FolderSync;
- SMB/NFS workflows;
- rsync;
- another product.

Provider-specific presets may populate ignore patterns but must not leak into core routing logic.

## Safety invariant

For `SafeMove`:

```text
Source delete allowed
IFF
Destination exists
AND destination length == source length
AND destination SHA-256 == source SHA-256
AND committed operation is persisted
```

This invariant should be enforced in one central service and covered by tests.

## Recovery

All operation state transitions are durable in SQLite. Reconciliation is authoritative; filesystem watchers are accelerators only.
