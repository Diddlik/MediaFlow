# Security policy

MomentFerry is a self-hosted service that can be configured to delete source files after a verified Safe Move. Treat access to the MomentFerry Web/API endpoint and its mounted filesystem Shares as privileged.

## Deployment guidance

- Keep MomentFerry on a trusted LAN or behind an authenticated reverse proxy/VPN.
- Do not expose the current unauthenticated Web UI/API directly to the public Internet.
- Mount only the source and destination directories MomentFerry actually needs.
- Keep `MomentFerry:AllowedRoots` narrow.
- Run with **Dry Run enabled** until routing preview has been verified with representative files.
- Keep the `/app/data` directory backed up.
- Protect MQTT with authentication and TLS when it crosses an untrusted network.
- Do not commit MQTT passwords, reverse-proxy credentials, tokens or other secrets to this repository.
- Keep the container image and NuGet dependencies updated.

## Destructive-operation invariant

For Safe Move, source deletion is allowed only after MomentFerry has established and persisted a verified destination. The implementation and automated tests are designed around this invariant:

```text
Source delete allowed
IFF
Destination exists
AND destination length == source length
AND destination SHA-256 == source SHA-256
AND committed operation state is persisted
```

If a state is ambiguous, MomentFerry must preserve the source.

## Filesystem trust boundary

MomentFerry assumes the configured filesystem and storage stack behave consistently enough for read-after-write verification. Hardware failure, filesystem corruption, malicious storage modification and external processes changing files concurrently remain outside the application's complete control.

Important production mitigations include:

- NAS snapshots/backups;
- filesystem integrity checks where available;
- reliable storage and RAID where appropriate;
- avoiding unrelated writers in MomentFerry destination directories;
- monitoring free space.

## Reporting a vulnerability

Do not publish sensitive exploit details in a public issue before a fix is available. Use GitHub's private vulnerability reporting/security-advisory mechanism for this repository when available. If private reporting is not available, open a minimal issue requesting a private contact channel without including exploit details.

Include:

- affected MomentFerry version/commit;
- deployment environment;
- reproduction conditions;
- expected and observed behavior;
- whether source-file deletion or path traversal is involved.

## Supported versions

Until the first stable release, security fixes target the current `main` branch and the most recent published container image. A formal stable-version support policy will be defined for v1.0.
