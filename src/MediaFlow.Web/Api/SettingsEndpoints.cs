using MediaFlow.Application.Abstractions;
using MediaFlow.Core.Domain;
using MediaFlow.Infrastructure;
using MediaFlow.Web.Background;

namespace MediaFlow.Web.Api;

public static class SettingsEndpoints
{
    private const string LiveModeConfirmation = "ENABLE_LIVE_TRANSFERS";

    public static IEndpointRouteBuilder MapSettingsEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/settings", async (
            IRuntimeSettingsStore store,
            CancellationToken ct) =>
            Results.Ok(await store.GetAsync(ct)));

        app.MapPut("/api/v1/settings", async (
            RuntimeSettingsRequest request,
            IRuntimeSettingsStore store,
            CancellationToken ct) =>
        {
            if (request.ReconciliationIntervalSeconds is < 15 or > 86400)
                return Results.BadRequest(new { error = "ReconciliationIntervalSeconds must be between 15 and 86400." });
            if (request.MaxFilesPerSharePerCycle is < 1 or > 2000)
                return Results.BadRequest(new { error = "MaxFilesPerSharePerCycle must be between 1 and 2000." });

            var current = await store.GetAsync(ct);
            if (current.DryRun && !request.DryRun &&
                !string.Equals(request.LiveModeConfirmation, LiveModeConfirmation, StringComparison.Ordinal))
            {
                return Results.Conflict(new
                {
                    error = $"Switching from Dry Run to Live requires confirmation token '{LiveModeConfirmation}'."
                });
            }

            var updated = await store.UpdateAsync(new MediaFlowRuntimeSettings(
                request.DryRun,
                request.AutomationEnabled,
                request.ReconciliationIntervalSeconds,
                request.MaxFilesPerSharePerCycle,
                request.AllowFilesystemTimestampFallback), ct);

            return Results.Ok(updated);
        });

        app.MapDelete("/api/v1/settings", async (
            IRuntimeSettingsStore store,
            CancellationToken ct) => Results.Ok(await store.ResetAsync(ct)));

        app.MapGet("/api/v1/status", async (
            IRuntimeSettingsStore store,
            AutomationStatus automationStatus,
            CancellationToken ct) =>
        {
            var settings = await store.GetAsync(ct);
            return Results.Ok(new
            {
                mode = settings.DryRun ? "dry-run" : "live",
                settings.AutomationEnabled,
                settings.ReconciliationIntervalSeconds,
                settings.MaxFilesPerSharePerCycle,
                settings.AllowFilesystemTimestampFallback,
                automation = automationStatus.Snapshot()
            });
        });

        app.MapGet("/api/v1/storage", async (
            IShareRepository shares,
            IFileSystemGateway fileSystem,
            CancellationToken ct) =>
        {
            var destinations = (await shares.ListAsync(ct))
                .Where(x => x.Enabled && x.Role is ShareRole.Destination or ShareRole.Both)
                .ToArray();
            var items = new List<StorageShareStatus>(destinations.Length);

            foreach (var share in destinations)
            {
                long? freeBytes = null;
                string? error = null;
                try
                {
                    freeBytes = fileSystem.GetAvailableFreeSpace(share.Path);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
                {
                    error = ex.Message;
                }

                items.Add(new StorageShareStatus(
                    share.Id,
                    share.Name,
                    share.Path,
                    fileSystem.DirectoryExists(share.Path),
                    freeBytes,
                    freeBytes is long value && value < LocalFileSystemGateway.MinimumFreeSpaceReserveBytes,
                    error));
            }

            return Results.Ok(new
            {
                minimumFreeSpaceReserveBytes = LocalFileSystemGateway.MinimumFreeSpaceReserveBytes,
                items
            });
        });

        return app;
    }
}

public sealed record RuntimeSettingsRequest(
    bool DryRun,
    bool AutomationEnabled,
    int ReconciliationIntervalSeconds,
    int MaxFilesPerSharePerCycle,
    bool AllowFilesystemTimestampFallback,
    string? LiveModeConfirmation = null);

public sealed record StorageShareStatus(
    Guid ShareId,
    string Name,
    string Path,
    bool Exists,
    long? AvailableFreeSpaceBytes,
    bool BelowReserve,
    string? Error);
