using MediaFlow.Application.Abstractions;

namespace MediaFlow.Web.Updates;

public sealed class ImageUpdateWorker(
    ImageUpdateService updates,
    IRuntimeSettingsStore settings,
    ILogger<ImageUpdateWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromHours(6));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            if (!(await settings.GetAsync(stoppingToken)).AutomaticImageUpdatesEnabled) continue;
            try
            {
                var status = await updates.CheckAsync(stoppingToken);
                if (status.UpdateAvailable && status.UpdaterConfigured)
                    await updates.InstallAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException)
            {
                logger.LogError(ex, "Automatic image update failed");
            }
        }
    }
}
