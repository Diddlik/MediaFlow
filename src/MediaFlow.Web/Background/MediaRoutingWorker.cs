using MediaFlow.Application.Abstractions;
using MediaFlow.Application.Services;
using MediaFlow.Core.Domain;

namespace MediaFlow.Web.Background;

public sealed class MediaRoutingWorker(
    IShareRepository shares,
    RoutingPreviewService routing,
    TransferCoordinator transfers,
    IRuntimeSettingsStore runtimeSettings,
    AutomationStatus status,
    AutomationWakeSignal wakeSignal,
    IClock clock,
    IConfiguration configuration,
    ILogger<MediaRoutingWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var initialDelay = TimeSpan.FromSeconds(
            Math.Clamp(configuration.GetValue("MediaFlow:Automation:InitialDelaySeconds", 10), 0, 300));
        if (initialDelay > TimeSpan.Zero)
            await Task.Delay(initialDelay, stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            var settings = await runtimeSettings.GetAsync(stoppingToken);
            if (settings.AutomationEnabled)
            {
                status.CycleStarted(clock.UtcNow);
                try
                {
                    var result = await ProcessCycleAsync(settings, stoppingToken);
                    status.CycleCompleted(
                        clock.UtcNow,
                        result.SourceShares,
                        result.Matched,
                        result.WouldMove,
                        result.Executed,
                        result.Skipped,
                        result.Errors);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    status.CycleFailed(clock.UtcNow, ex.Message);
                    logger.LogError(ex, "Unhandled error in MediaFlow automation cycle");
                }
            }

            settings = await runtimeSettings.GetAsync(stoppingToken);
            await wakeSignal.WaitAsync(
                TimeSpan.FromSeconds(settings.ReconciliationIntervalSeconds),
                stoppingToken);
        }
    }

    private async Task<CycleResult> ProcessCycleAsync(
        MediaFlowRuntimeSettings settings,
        CancellationToken cancellationToken)
    {
        var matched = 0;
        var wouldMove = 0;
        var executed = 0;
        var skipped = 0;
        var errors = 0;

        var sourceShares = (await shares.ListAsync(cancellationToken))
            .Where(x => x.Enabled && x.Role is ShareRole.Source or ShareRole.Both)
            .ToArray();

        foreach (var sourceShare in sourceShares)
        {
            cancellationToken.ThrowIfCancellationRequested();
            IReadOnlyList<RoutingPreviewItem> items;
            try
            {
                items = await routing.PreviewAsync(
                    sourceShare,
                    settings.MaxFilesPerSharePerCycle,
                    cancellationToken,
                    settings.MaxParallelMetadataReads,
                    progress => status.Progress(
                        sourceShare.Name,
                        progress.Phase,
                        progress.Processed,
                        progress.Total));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
            {
                errors++;
                logger.LogWarning(ex, "Automation scan failed for share {Share}", sourceShare.Name);
                continue;
            }

            foreach (var item in items.Where(x => x.State == RoutingPreviewState.Matched && x.Event is not null))
            {
                cancellationToken.ThrowIfCancellationRequested();
                matched++;
                status.MatchFound();

                if (!settings.AllowFilesystemTimestampFallback &&
                    string.Equals(item.MediaFile.TimestampSource, "FileLastWriteTimeUtc", StringComparison.Ordinal))
                {
                    skipped++;
                    logger.LogInformation(
                        "Auto-routing skipped {File}: capture time is filesystem fallback",
                        item.MediaFile.SourcePath);
                    continue;
                }

                wouldMove++;
                status.WouldMoveFound();

                if (settings.DryRun)
                {
                    skipped++;
                    logger.LogInformation(
                        "Dry run: would route {Source} to {Destination} for event {Event}",
                        item.MediaFile.SourcePath,
                        item.DestinationPath,
                        item.Event!.Name);
                    continue;
                }

                try
                {
                    var result = await transfers.ExecuteOnceAsync(
                        item.MediaFile.Id,
                        item.Event!.Id,
                        cancellationToken);

                    if (result.Executed)
                    {
                        executed++;
                        logger.LogInformation(
                            "Auto-routing completed {Source}: {State}",
                            item.MediaFile.SourcePath,
                            result.Result?.Operation.State);
                    }
                    else
                    {
                        skipped++;
                    }
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or NotSupportedException)
                {
                    errors++;
                    logger.LogWarning(ex, "Auto-routing failed for {File}", item.MediaFile.SourcePath);
                }
            }
        }

        return new CycleResult(sourceShares.Length, matched, wouldMove, executed, skipped, errors);
    }

    private sealed record CycleResult(
        int SourceShares,
        int Matched,
        int WouldMove,
        int Executed,
        int Skipped,
        int Errors);
}
