using MomentFerry.Core.Domain;

namespace MomentFerry.Application.Abstractions;

public interface IMediaFileRepository
{
    Task<IReadOnlyList<MediaFile>> ListRecentAsync(int limit = 200, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MediaFile>> ListBySourceAsync(Guid sourceShareId, CancellationToken cancellationToken = default);
    Task<MediaFile?> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<MediaFile?> GetBySourceAsync(Guid sourceShareId, string sourcePath, CancellationToken cancellationToken = default);
    Task UpsertAsync(MediaFile mediaFile, CancellationToken cancellationToken = default);
}
