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
        CancellationToken cancellationToken = default)
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

        var groups = (await sourceGroups.ListAsync(cancellationToken))
            .ToDictionary(x => x.Id);
        var allShares = (await shares.ListAsync(cancellationToken))
            .ToDictionary(x => x.Id);

        var result = new List<RoutingPreviewItem>(stableFiles.Length);
        foreach (var file in stableFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var metadata = await metadataExtractor.ExtractAsync(
                sourceShare,
                file.FullPath,
                file.MediaType,
                cancellationToken);

            var capturedAt = metadata.CapturedAt ?? file.LastWriteUtc;
            var timestampSource = metadata.TimestampSource ?? "FileLastWriteTimeUtc";
            var fallbackMessage = metadata.CapturedAt is null && metadata.Error is not null
                ? $"Metadata unavailable; FileLastWriteTimeUtc used as fallback. {metadata.Error}"
                : null;
            indexedFiles.TryGetValue(file.FullPath, out var existing);
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
                IsTimezoneInferred = metadata.TimeZoneInferred || metadata.CapturedAt is null,
                Sha256 = existing?.Sha256,
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
                continue;
            }

            var destinationPath = destinationPaths.Resolve(matchedEvent, sourceShare, destinationShare, mediaFile);
            result.Add(new RoutingPreviewItem(
                mediaFile,
                RoutingPreviewState.Matched,
                matchedEvent,
                destinationPath,
                fallbackMessage));
        }

        return result;
    }
}

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
