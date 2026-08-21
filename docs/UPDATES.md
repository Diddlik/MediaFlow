# Image updates

MediaFlow checks the latest stable GitHub release and shows its version and changelog in the Web UI. Manual mode is the default. Automatic stable-image updates are opt-in.

## Updater boundary

MediaFlow never receives the Docker socket. A separate Watchtower container owns that privileged mount and exposes only its authenticated update endpoint on the internal Compose network. Label and scope filters restrict it to the MediaFlow container.

Configure these values on the `mediaflow` service:

```yaml
environment:
  MediaFlow__Updates__WatchtowerUrl: http://mediaflow-updater:8080
  MediaFlow__Updates__WatchtowerToken: "replace-with-a-long-random-token"
```

Enable the commented `mediaflow-updater` service in `docker-compose.example.yml` and give `WATCHTOWER_HTTP_API_TOKEN` the same token. Do not publish the updater port to the host.

The example deliberately does not enable Watchtower cleanup, so the previous image remains available for manual rollback.

## UI workflow

1. Select **Check for updates**.
2. Review the stable version and changelog.
3. Select **Install update** and enter `INSTALL_UPDATE`.
4. The companion pulls the configured image and recreates MediaFlow.

After the replacement container answers its first status request with the expected release version, MediaFlow records the update as completed in `data/update-status.json`.

Automatic mode checks every six hours and requests installation when a newer stable semantic version is available.

## Rollback policy

Watchtower is archived and does not provide health-gated automatic rollback. A second custom Docker-socket service solely for rollback would widen the privileged attack surface, so it is deliberately not part of the supported update path. The updater retains the previous image and MediaFlow uses deterministic, operator-triggered rollback instead.

Record the current version before an update. To restore it with the example Compose file:

```powershell
$env:MEDIAFLOW_IMAGE = "ghcr.io/diddlik/mediaflow:1.0.0"
docker compose pull mediaflow
docker compose up -d --no-deps mediaflow
docker compose ps mediaflow
```

Replace `1.0.0` with the known-good tag. Wait for the container to report `healthy`, then verify `/health`, storage status and routing preview before enabling Live mode. Restore `/app/data` only when the release notes describe an incompatible persistent-state change; follow [Backup and restore](BACKUP-RESTORE.md).
