using MomentFerry.Core.Domain;

namespace MomentFerry.Application.Abstractions;

public interface IMediaMetadataExtractor
{
    Task<MediaMetadata> ExtractAsync(
        Share share,
        string path,
        MediaType mediaType,
        CancellationToken cancellationToken = default);
}

public sealed record MediaMetadata(
    DateTimeOffset? CapturedAt,
    string? TimestampSource,
    bool TimeZoneInferred,
    string? CameraMake,
    string? CameraModel,
    int? Width,
    int? Height,
    double? DurationSeconds,
    string? MimeType,
    string? Error = null);
