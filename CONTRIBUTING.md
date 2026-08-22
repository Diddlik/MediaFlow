# Contributing to MomentFerry

MomentFerry can perform destructive filesystem operations, so changes to routing, verification, recovery and source deletion require a higher review bar than ordinary application code.

## Development setup

Requirements:

- .NET 10 SDK
- Docker for container validation
- ExifTool for local metadata integration tests/manual testing

Build and test:

```bash
dotnet restore MomentFerry.sln
dotnet build MomentFerry.sln -c Release
dotnet test MomentFerry.sln -c Release
docker build -t momentferry:dev .
```

## Architecture boundaries

Keep the dependency direction:

```text
MomentFerry.Core
      ↑
MomentFerry.Application
      ↑
MomentFerry.Infrastructure
      ↑
MomentFerry.Web
```

Core/Application must remain independent of specific sync products. Resilio Sync, Syncthing, Nextcloud and similar tools are external to MomentFerry; filesystem Shares are the integration boundary.

## Safety requirements

Any change involving Safe Move, duplicate handling, recovery, retry, conflict resolution or source deletion must preserve this invariant:

```text
Source delete allowed
IFF
Destination exists
AND destination length == source length
AND destination SHA-256 == source SHA-256
AND committed operation state is persisted
```

Required practices:

- never add a direct move/delete shortcut that bypasses verification;
- ambiguous/error states preserve the source;
- do not overwrite non-identical destination files by default;
- keep operations idempotent across retries/restarts;
- add automated failure-path tests before changing source-deletion behavior;
- keep Dry Run as the default deployment mode.

## Pull requests

Prefer focused changes. A PR should explain:

- what behavior changes;
- whether filesystem/destructive behavior is affected;
- how it was tested;
- any migration/deployment impact.

CI must pass:

1. restore;
2. Release build;
3. automated tests;
4. Docker image build.

## Database changes

Until a formal migration framework is introduced, database schema changes must be backwards-conscious and initialization must remain idempotent. Do not silently drop columns/tables or destroy existing state.

## Dependencies

Keep external dependencies minimal. Before adding a package, confirm that the functionality cannot reasonably live in the BCL/current stack and that the package is actively maintained. Address dependency security advisories promptly.

## UI/API changes

- preserve mobile usability;
- expose errors without leaking secrets;
- destructive mode changes require explicit confirmation;
- keep REST/MQTT behavior routed through shared application services rather than duplicating business rules in integrations.
