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
    IClock clock)
{
    public async Task<IReadOnlyList<RoutingPreviewItem>> PreviewAsync(
        Share sourceShare,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var stableFiles = discovery.Scan(sourceShare, Math.Max(limit * 5, limit))
            .Where(x => x.State == DiscoveryState.Stable)
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
            var existing = await mediaFiles.GetBySourceAsync(sourceShare.Id, file.FullPath, cancellationToken);
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
                IsTimezoneInferred = metadata.TimeZoneInferred,
                Sha256 = existing?.Sha256,
                FirstSeenAt = existing?.FirstSeenAt ?? now,
                LastSeenAt = now
            };
            await mediaFiles.UpsertAsync(mediaFile, cancellationToken);

            if (metadata.Error is not null && metadata.CapturedAt is null)
            {
                result.Add(new RoutingPreviewItem(
                    mediaFile,
                    RoutingPreviewState.MetadataFallback,
                    null,
                    null,
                    metadata.Error));
                continue;
            }

            var candidateEvents = await events.ListMatchableAsync(capturedAt, cancellationToken);
            var matches = candidateEvents
                .Where(e => groups.TryGetValue(e.SourceGroupId, out var group) && group.ShareIds.Contains(sourceShare.Id))
                .ToArray();

            if (matches.Length == 0)
            {
                result.Add(new RoutingPreviewItem(mediaFile, RoutingPreviewState.Unmatched, null, null, null));
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

            var destinationPath = BuildDestinationPath(matchedEvent, sourceShare, destinationShare, mediaFile);
            result.Add(new RoutingPreviewItem(
                mediaFile,
                RoutingPreviewState.Matched,
                matchedEvent,
                destinationPath,
                null));
        }

        return result;
    }

    private static string BuildDestinationPath(
        MediaEvent mediaEvent,
        Share sourceShare,
        Share destinationShare,
        MediaFile mediaFile)
    {
        var captured = mediaFile.CapturedAt ?? DateTimeOffset.UtcNow;
        var folder = mediaEvent.DestinationFolderTemplate
            .Replace("{event.name}", SafeSegment(mediaEvent.Name), StringComparison.OrdinalIgnoreCase)
            .Replace("{event.type}", SafeSegment(mediaEvent.Type ?? "Event"), StringComparison.OrdinalIgnoreCase)
            .Replace("{year}", captured.Year.ToString("0000"), StringComparison.OrdinalIgnoreCase)
            .Replace("{month}", captured.Month.ToString("00"), StringComparison.OrdinalIgnoreCase)
            .Replace("{day}", captured.Day.ToString("00"), StringComparison.OrdinalIgnoreCase)
            .Replace("{source}", SafeSegment(sourceShare.Name), StringComparison.OrdinalIgnoreCase)
            .Replace("{owner}", SafeSegment(sourceShare.Owner ?? sourceShare.Name), StringComparison.OrdinalIgnoreCase);

        var root = Path.GetFullPath(destinationShare.Path);
        var combined = Path.GetFullPath(Path.Combine(root, folder, mediaFile.OriginalName));
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        var rootWithSeparator = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!combined.StartsWith(rootWithSeparator, comparison))
        {
            throw new InvalidOperationException("Destination template escapes the configured destination share.");
        }

        return combined;
    }

    private static string SafeSegment(string value)
    {
        var invalid = Path.GetInvalidFileNameChars().ToHashSet();
        var cleaned = new string(value.Trim().Select(c => invalid.Contains(c) ? '_' : c).ToArray());
        return string.IsNullOrWhiteSpace(cleaned) ? "unnamed" : cleaned;
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
