using MediaFlow.Application.Abstractions;
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
