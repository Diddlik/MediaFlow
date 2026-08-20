using MediaFlow.Application.Abstractions;
using MediaFlow.Core.Domain;

namespace MediaFlow.Application.Services;

public sealed class MetadataPreviewService(
    ShareDiscoveryService discovery,
    IMediaMetadataExtractor metadataExtractor)
{
    public async Task<IReadOnlyList<MetadataPreviewItem>> PreviewAsync(
        Share share,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var stableFiles = discovery.Scan(share, Math.Max(limit * 5, limit))
            .Where(x => x.State == DiscoveryState.Stable)
            .Take(limit)
            .ToArray();

        var result = new List<MetadataPreviewItem>(stableFiles.Length);
        foreach (var file in stableFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var metadata = await metadataExtractor.ExtractAsync(
                share,
                file.FullPath,
                file.MediaType,
                cancellationToken);

            result.Add(new MetadataPreviewItem(file, metadata));
        }

        return result;
    }
}

public sealed record MetadataPreviewItem(
    DiscoveredFile File,
    MediaMetadata Metadata);
