using MomentFerry.Core.Domain;

namespace MomentFerry.Application.Abstractions;

public interface IMediaOperationRepository
{
    Task<IReadOnlyList<MediaOperation>> ListRecentAsync(int limit = 200, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MediaOperation>> ListByStateAsync(MediaOperationState state, int limit = 200, CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<MediaOperationState, long>> CountByStateAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MediaOperation>> ListIncompleteAsync(CancellationToken cancellationToken = default);
    Task<MediaOperation?> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<MediaOperation?> GetIncompleteByMediaFileAsync(Guid mediaFileId, CancellationToken cancellationToken = default);
    Task<bool> HasTerminalOperationAsync(Guid mediaFileId, Guid eventId, CancellationToken cancellationToken = default);
    Task UpsertAsync(MediaOperation operation, CancellationToken cancellationToken = default);
}
