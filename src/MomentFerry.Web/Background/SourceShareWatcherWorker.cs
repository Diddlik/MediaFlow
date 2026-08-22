using System.Collections.Concurrent;
using MomentFerry.Application.Abstractions;
using MomentFerry.Core.Domain;

namespace MomentFerry.Web.Background;

public sealed class SourceShareWatcherWorker(
    IShareRepository shares,
    AutomationWakeSignal wakeSignal,
    IConfiguration configuration,
    ILogger<SourceShareWatcherWorker> logger) : BackgroundService
{
    private readonly Dictionary<Guid, WatchRegistration> _watchers = [];
    private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _stabilityWakeups = new();
    private readonly object _watcherGate = new();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var refreshSeconds = Math.Clamp(
            configuration.GetValue("MomentFerry:Automation:WatcherRefreshSeconds", 30),
            5,
            3600);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ReconcileWatchersAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Source watcher reconciliation failed");
            }

            await Task.Delay(TimeSpan.FromSeconds(refreshSeconds), stoppingToken);
        }
    }

    public override void Dispose()
    {
        lock (_watcherGate)
        {
            foreach (var registration in _watchers.Values)
                registration.Watcher.Dispose();
            _watchers.Clear();
        }

        foreach (var cancellation in _stabilityWakeups.Values)
        {
            cancellation.Cancel();
            cancellation.Dispose();
        }
        _stabilityWakeups.Clear();
        base.Dispose();
    }

    private async Task ReconcileWatchersAsync(CancellationToken cancellationToken)
    {
        var desired = (await shares.ListAsync(cancellationToken))
            .Where(x => x.Enabled && x.Role is ShareRole.Source or ShareRole.Both)
            .ToDictionary(x => x.Id);

        lock (_watcherGate)
        {
            foreach (var obsoleteId in _watchers.Keys.Where(id => !desired.ContainsKey(id)).ToArray())
                RemoveWatcher(obsoleteId);

            foreach (var share in desired.Values)
            {
                var fingerprint = new WatchFingerprint(
                    Path.GetFullPath(share.Path),
                    share.Recursive,
                    share.StabilitySeconds);

                if (_watchers.TryGetValue(share.Id, out var existing) && existing.Fingerprint == fingerprint)
                    continue;

                if (existing is not null) RemoveWatcher(share.Id);
                TryAddWatcher(share, fingerprint);
            }
        }
    }

    private void TryAddWatcher(Share share, WatchFingerprint fingerprint)
    {
        if (!Directory.Exists(fingerprint.Path))
        {
            logger.LogDebug("Source share path {Path} does not exist yet; watcher will retry later", fingerprint.Path);
            return;
        }

        try
        {
            var watcher = new FileSystemWatcher(fingerprint.Path)
            {
                IncludeSubdirectories = fingerprint.Recursive,
                NotifyFilter = NotifyFilters.FileName |
                               NotifyFilters.DirectoryName |
                               NotifyFilters.LastWrite |
                               NotifyFilters.Size,
                InternalBufferSize = 64 * 1024,
                EnableRaisingEvents = false
            };

            FileSystemEventHandler changed = (_, args) => OnFileSystemChanged(share.Id, fingerprint.StabilitySeconds, args.FullPath);
            RenamedEventHandler renamed = (_, args) => OnFileSystemChanged(share.Id, fingerprint.StabilitySeconds, args.FullPath);
            ErrorEventHandler error = (_, args) =>
            {
                logger.LogWarning(args.GetException(), "FileSystemWatcher error for share {Share}", share.Name);
                wakeSignal.Wake();
            };

            watcher.Created += changed;
            watcher.Changed += changed;
            watcher.Deleted += changed;
            watcher.Renamed += renamed;
            watcher.Error += error;
            watcher.EnableRaisingEvents = true;

            _watchers[share.Id] = new WatchRegistration(watcher, fingerprint);
            logger.LogInformation("Watching source share {Share} at {Path}", share.Name, fingerprint.Path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            logger.LogWarning(ex, "Cannot watch source share {Share} at {Path}; periodic reconciliation remains active", share.Name, fingerprint.Path);
        }
    }

    private void RemoveWatcher(Guid shareId)
    {
        if (!_watchers.Remove(shareId, out var registration)) return;
        registration.Watcher.EnableRaisingEvents = false;
        registration.Watcher.Dispose();

        if (_stabilityWakeups.TryRemove(shareId, out var cancellation))
        {
            cancellation.Cancel();
            cancellation.Dispose();
        }
    }

    private void OnFileSystemChanged(Guid shareId, int stabilitySeconds, string path)
    {
        logger.LogDebug("Filesystem change detected for share {ShareId}: {Path}", shareId, path);

        // First scan records the current file size/write timestamp immediately.
        wakeSignal.Wake();

        // A second scan after the last observed filesystem change allows the
        // discovery service to confirm that the file stayed unchanged for the
        // configured stability interval.
        var next = new CancellationTokenSource();
        var previous = _stabilityWakeups.AddOrUpdate(
            shareId,
            next,
            (_, existing) =>
            {
                existing.Cancel();
                existing.Dispose();
                return next;
            });

        if (!ReferenceEquals(previous, next)) return;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(Math.Clamp(stabilitySeconds + 1, 2, 3601)), next.Token);
                wakeSignal.Wake();
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                if (_stabilityWakeups.TryGetValue(shareId, out var current) && ReferenceEquals(current, next))
                    _stabilityWakeups.TryRemove(shareId, out _);
                next.Dispose();
            }
        });
    }

    private sealed record WatchFingerprint(string Path, bool Recursive, int StabilitySeconds);
    private sealed record WatchRegistration(FileSystemWatcher Watcher, WatchFingerprint Fingerprint);
}
