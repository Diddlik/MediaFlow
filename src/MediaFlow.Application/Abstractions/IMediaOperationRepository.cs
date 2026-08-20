using MediaFlow.Core.Domain;

namespace MediaFlow.Application.Abstractions;

public interface IMediaOperationRepository
{
    Task<IReadOnlyList<MediaOperation>> ListRecentAsync(int limit = 200, CancellationToken cancellationToken = default);
    Task<MediaOperation?> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<MediaOperation?> GetIncompleteByMediaFileAsync(Guid mediaFileId, CancellationToken cancellationToken = default);
    Task UpsertAsync(MediaOperation operation, CancellationToken cancellationToken = default);
}
