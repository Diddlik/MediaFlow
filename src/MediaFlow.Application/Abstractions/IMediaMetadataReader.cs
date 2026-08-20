using MediaFlow.Core.Domain;

namespace MediaFlow.Application.Abstractions;

public sealed record MediaMetadata(
    MediaType MediaType,
    DateTimeOffset? CapturedAt,
    string? TimestampSource,
    bool IsTimezoneInferred,
    string? CameraMake = null,
    string? CameraModel = null,
    int? Width = null,
    int? Height = null,
    TimeSpan? Duration = null);

public interface IMediaMetadataReader
{
    Task<MediaMetadata> ReadAsync(string path, string? defaultTimeZone, CancellationToken cancellationToken = default);
}
