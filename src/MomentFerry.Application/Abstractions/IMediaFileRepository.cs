using MomentFerry.Core.Domain;

namespace MomentFerry.Application.Abstractions;

public interface IMediaFileRepository
{
    Task<IReadOnlyList<MediaFile>> ListRecentAsync(int limit = 200, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MediaFile>> ListBySourceAsync(Guid sourceShareId, CancellationToken cancellationToken = default);
    Task<MediaFile?> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<MediaFile?> GetBySourceAsync(Guid sourceShareId, string sourcePath, CancellationToken cancellationToken = default);
    Task UpsertAsync(MediaFile mediaFile, CancellationToken cancellationToken = default);

    /// <summary>
    /// Pushes indexed files whose capture time falls inside an event window back to the front of the
    /// routing queue, so an event created or edited after the media arrived is applied on the next
    /// cycle instead of waiting for the least-recently-evaluated sweep to reach them.
    /// </summary>
    Task<int> RequeueByCaptureWindowAsync(
        IReadOnlyCollection<Guid> sourceShareIds,
        DateTimeOffset startAt,
        DateTimeOffset? endAt,
        CancellationToken cancellationToken = default);
}
