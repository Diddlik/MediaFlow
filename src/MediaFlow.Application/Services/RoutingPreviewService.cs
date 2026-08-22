using MediaFlow.Application.Abstractions;
using MediaFlow.Core.Domain;

namespace MediaFlow.Application.Services;

public sealed class RoutingPreviewService(
    ShareDiscoveryService discovery,
    IMediaMetadataExtractor metadataExtractor,
    IMediaFileRepository mediaFiles,
    IMediaEventRepository events,
    ISourceGroupRepository sourceGroups,
    IShareRepository shares,
    DestinationPathResolver destinationPaths,
    IClock clock)
{
    public async Task<IReadOnlyList<RoutingPreviewItem>> PreviewAsync(
        Share sourceShare,
        int limit,
        CancellationToken cancellationToken = default,
        int maxParallelMetadataReads = 1,
        Action<RoutingPreviewProgress>? progress = null)
    {
        var indexedFiles = (await mediaFiles.ListBySourceAsync(sourceShare.Id, cancellationToken))
            .ToDictionary(x => x.SourcePath, StringComparer.Ordinal);
        var stableFiles = discovery.Enumerate(sourceShare)
            .Where(x => x.State == DiscoveryState.Stable)
            .OrderBy(x => indexedFiles.TryGetValue(x.FullPath, out var indexed)
                ? indexed.LastSeenAt
                : DateTimeOffset.MinValue)
            .ThenBy(x => x.FullPath, StringComparer.Ordinal)
            .Take(limit)
            .ToArray();

        var metadata = new MediaMetadata?[stableFiles.Length];
        var cached = 0;
        var pending = new List<int>(stableFiles.Length);
        for (var index = 0; index < stableFiles.Length; index++)
        {
            var file = stableFiles[index];
            if (indexedFiles.TryGetValue(file.FullPath, out var existing) &&
                existing.Size == file.Size &&
                existing.SourceLastWriteAt == file.LastWriteUtc &&
                existing.CapturedAt is not null)
            {
                cached++;
            }
            else
            {
                pending.Add(index);
            }
        }

        progress?.Invoke(new RoutingPreviewProgress("Reading metadata", cached, stableFiles.Length));
        var metadataRead = 0;
        await Parallel.ForEachAsync(
            pending,
            new ParallelOptions
            {
                MaxDegreeOfParallelism = Math.Clamp(maxParallelMetadataReads, 1, 8),
                CancellationToken = cancellationToken
            },
            async (index, ct) =>
            {
                var file = stableFiles[index];
                metadata[index] = await metadataExtractor.ExtractAsync(
                    sourceShare,
                    file.FullPath,
                    file.MediaType,
                    ct);
                var completed = cached + Interlocked.Increment(ref metadataRead);
                progress?.Invoke(new RoutingPreviewProgress("Reading metadata", completed, stableFiles.Length));
            });

        var groups = (await sourceGroups.ListAsync(cancellationToken))
            .ToDictionary(x => x.Id);
        var allShares = (await shares.ListAsync(cancellationToken))
            .ToDictionary(x => x.Id);

        var result = new List<RoutingPreviewItem>(stableFiles.Length);
        for (var index = 0; index < stableFiles.Length; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var file = stableFiles[index];
            indexedFiles.TryGetValue(file.FullPath, out var existing);
            var extracted = metadata[index];
            var capturedAt = extracted?.CapturedAt ?? existing?.CapturedAt ?? file.LastWriteUtc;
            var timestampSource = extracted?.TimestampSource ?? existing?.TimestampSource ?? "FileLastWriteTimeUtc";
            var fallbackMessage = extracted?.CapturedAt is null && extracted?.Error is not null
                ? $"Metadata unavailable; FileLastWriteTimeUtc used as fallback. {extracted.Error}"
                : null;
            var now = clock.UtcNow;

            var mediaFile = new MediaFile
            {
                Id = existing?.Id ?? Guid.NewGuid(),
                SourceShareId = sourceShare.Id,
                SourcePath = file.FullPath,
                OriginalName = Path.GetFileName(file.FullPath),
                Size = file.Size,
                Extension = Path.GetExtension(file.FullPath),
                MediaType = file.MediaType,
                CapturedAt = capturedAt,
                TimestampSource = timestampSource,
                IsTimezoneInferred = extracted is null
                    ? existing?.IsTimezoneInferred ?? true
                    : extracted.TimeZoneInferred || extracted.CapturedAt is null,
                Sha256 = existing?.Sha256,
                SourceLastWriteAt = file.LastWriteUtc,
                FirstSeenAt = existing?.FirstSeenAt ?? now,
                LastSeenAt = now
            };
            await mediaFiles.UpsertAsync(mediaFile, cancellationToken);

            var candidateEvents = await events.ListMatchableAsync(capturedAt, cancellationToken);
            var matches = candidateEvents
                .Where(e => groups.TryGetValue(e.SourceGroupId, out var group) && group.ShareIds.Contains(sourceShare.Id))
                .ToArray();

            if (matches.Length == 0)
            {
                result.Add(new RoutingPreviewItem(mediaFile, RoutingPreviewState.Unmatched, null, null, fallbackMessage));
                progress?.Invoke(new RoutingPreviewProgress("Matching events", index + 1, stableFiles.Length));
                continue;
            }

            if (matches.Length > 1)
            {
                result.Add(new RoutingPreviewItem(
                    mediaFile,
                    RoutingPreviewState.Ambiguous,
                    null,
                    null,
                    $"File matches {matches.Length} events."));
                progress?.Invoke(new RoutingPreviewProgress("Matching events", index + 1, stableFiles.Length));
                continue;
            }

            var matchedEvent = matches[0];
            if (!allShares.TryGetValue(matchedEvent.DestinationShareId, out var destinationShare) ||
                !destinationShare.Enabled ||
                destinationShare.Role == ShareRole.Source)
            {
                result.Add(new RoutingPreviewItem(
                    mediaFile,
                    RoutingPreviewState.InvalidDestination,
                    matchedEvent,
                    null,
                    "Destination share is missing, disabled, or not writable by role."));
                progress?.Invoke(new RoutingPreviewProgress("Matching events", index + 1, stableFiles.Length));
                continue;
            }

            var destinationPath = destinationPaths.Resolve(matchedEvent, sourceShare, destinationShare, mediaFile);
            result.Add(new RoutingPreviewItem(
                mediaFile,
                RoutingPreviewState.Matched,
                matchedEvent,
                destinationPath,
                fallbackMessage));
            progress?.Invoke(new RoutingPreviewProgress("Matching events", index + 1, stableFiles.Length));
        }

        return result;
    }
}

public sealed record RoutingPreviewProgress(string Phase, int Processed, int Total);

public sealed record RoutingPreviewItem(
    MediaFile MediaFile,
    RoutingPreviewState State,
    MediaEvent? Event,
    string? DestinationPath,
    string? Message);

public enum RoutingPreviewState
{
    Matched,
    Unmatched,
    Ambiguous,
    MetadataFallback,
    InvalidDestination
}
