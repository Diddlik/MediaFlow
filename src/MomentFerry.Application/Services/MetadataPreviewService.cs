using MomentFerry.Application.Abstractions;
using MomentFerry.Core.Domain;

namespace MomentFerry.Application.Services;

public sealed class MetadataPreviewService(
    ShareDiscoveryService discovery,
    IMediaMetadataExtractor metadataExtractor)
{
    public async Task<IReadOnlyList<MetadataPreviewItem>> PreviewAsync(
        Share share,
        int limit,
        CancellationToken cancellationToken = default,
        int maxParallelMetadataReads = 1)
    {
        var stableFiles = discovery.Scan(share, Math.Max(limit * 5, limit))
            .Where(x => x.State == DiscoveryState.Stable)
            .Take(limit)
            .ToArray();

        var result = new MetadataPreviewItem[stableFiles.Length];
        await Parallel.ForEachAsync(
            Enumerable.Range(0, stableFiles.Length),
            new ParallelOptions
            {
                MaxDegreeOfParallelism = Math.Clamp(maxParallelMetadataReads, 1, 8),
                CancellationToken = cancellationToken
            },
            async (index, ct) =>
            {
                var file = stableFiles[index];
                var metadata = await metadataExtractor.ExtractAsync(share, file.FullPath, file.MediaType, ct);
                result[index] = new MetadataPreviewItem(file, metadata);
            });

        return result;
    }
}

public sealed record MetadataPreviewItem(
    DiscoveredFile File,
    MediaMetadata Metadata);
