using MediaFlow.Application.Abstractions;
using MediaFlow.Application.Services;
using MediaFlow.Core.Domain;

namespace MediaFlow.Web.Background;

public sealed class MediaRoutingWorker(
    IShareRepository shares,
    RoutingPreviewService routing,
    TransferCoordinator transfers,
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
            if (configuration.GetValue("MediaFlow:Automation:Enabled", true))
            {
                try
                {
                    await ProcessCycleAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Unhandled error in MediaFlow automation cycle");
                }
            }

            var intervalSeconds = Math.Clamp(
                configuration.GetValue("MediaFlow:ReconciliationIntervalSeconds", 300),
                15,
                86400);
            await Task.Delay(TimeSpan.FromSeconds(intervalSeconds), stoppingToken);
        }
    }

    private async Task ProcessCycleAsync(CancellationToken cancellationToken)
    {
        var dryRun = configuration.GetValue("MediaFlow:DryRun", true);
        var allowFallback = configuration.GetValue(
            "MediaFlow:Automation:AllowFilesystemTimestampFallback",
            false);
        var maxFiles = Math.Clamp(
            configuration.GetValue("MediaFlow:Automation:MaxFilesPerSharePerCycle", 200),
            1,
            2000);

        var sourceShares = (await shares.ListAsync(cancellationToken))
            .Where(x => x.Enabled && x.Role is ShareRole.Source or ShareRole.Both)
            .ToArray();

        foreach (var sourceShare in sourceShares)
        {
            cancellationToken.ThrowIfCancellationRequested();
            IReadOnlyList<RoutingPreviewItem> items;
            try
            {
                items = await routing.PreviewAsync(sourceShare, maxFiles, cancellationToken);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
            {
                logger.LogWarning(ex, "Automation scan failed for share {Share}", sourceShare.Name);
                continue;
            }

            foreach (var item in items.Where(x => x.State == RoutingPreviewState.Matched && x.Event is not null))
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!allowFallback &&
                    string.Equals(item.MediaFile.TimestampSource, "FileLastWriteTimeUtc", StringComparison.Ordinal))
                {
                    logger.LogInformation(
                        "Auto-routing skipped {File}: capture time is filesystem fallback",
                        item.MediaFile.SourcePath);
                    continue;
                }

                if (dryRun)
                {
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
                        logger.LogInformation(
                            "Auto-routing completed {Source}: {State}",
                            item.MediaFile.SourcePath,
                            result.Result?.Operation.State);
                    }
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or NotSupportedException)
                {
                    logger.LogWarning(ex, "Auto-routing failed for {File}", item.MediaFile.SourcePath);
                }
            }
        }
    }
}
