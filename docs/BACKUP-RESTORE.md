# Backup and restore

MomentFerry keeps its persistent application state in the mounted `/app/data` directory.

A normal Docker Compose installation maps this to `./data` on the host:

```yaml
volumes:
  - ./data:/app/data
```

The directory contains at least:

- `momentferry.db` — SQLite configuration, media index, events and operation history;
- `momentferry.db-wal` / `momentferry.db-shm` — SQLite WAL files when present;
- `runtime-settings.json` — persistent Dry Run / automation runtime settings.

Media files themselves are **not** stored in this data directory. They remain in the configured source and destination Shares.

## Recommended backup

For the simplest consistent backup, stop MomentFerry briefly and copy the complete data directory as one unit.

```bash
docker compose stop momentferry
cp -a ./data ./backup/momentferry-data-$(date +%Y%m%d-%H%M%S)
docker compose start momentferry
```

On Synology or another NAS, a filesystem snapshot of the complete `data` directory while the container is stopped is also suitable.

Do not back up only `momentferry.db` while ignoring WAL files if the application is running.

## Online SQLite backup

If downtime is undesirable, use SQLite's backup command against the database from a host/container with a compatible `sqlite3` client:

```bash
sqlite3 ./data/momentferry.db ".backup './backup/momentferry.db'"
cp ./data/runtime-settings.json ./backup/runtime-settings.json
```

The `.backup` command creates a transactionally consistent database snapshot while MomentFerry is running.

## Restore

1. Stop MomentFerry.
2. Keep the current data directory as a rollback copy.
3. Restore the entire backed-up data directory.
4. Confirm ownership/permissions allow the container to read and write it.
5. Start MomentFerry.
6. Check `/health`, `/api/v1/status`, `/api/v1/storage`, Events and Operations before enabling Live mode.

Example:

```bash
docker compose stop momentferry
mv ./data ./data-before-restore
cp -a ./backup/momentferry-data-20260820-210000 ./data
docker compose start momentferry
```

## Recovery state after restore

MomentFerry performs operation recovery during startup. For incomplete operations it is intentionally conservative:

- a source is never deleted merely because an old operation says a copy was attempted;
- a committed destination is re-verified before a pending source deletion can continue;
- ambiguous states preserve the source and remain recoverable/quarantined.

Restoring the database without restoring or retaining the corresponding media Shares can therefore create missing-path states. Keep NAS Share backups/snapshots coordinated with the MomentFerry state backup when possible.

## Before upgrades

Before installing a new MomentFerry version:

1. keep Dry Run enabled for major configuration changes;
2. back up `/app/data`;
3. record the currently deployed image tag/digest;
4. update the image;
5. verify health, storage paths and routing preview;
6. enable Live mode only after verification.

For stable releases, prefer versioned image tags over `latest` so rollback is deterministic.
