using MomentFerry.Core.Domain;

namespace MomentFerry.Application.Abstractions;

public interface IMediaEventRepository
{
    Task<IReadOnlyList<MediaEvent>> ListAsync(CancellationToken cancellationToken = default);
    Task<MediaEvent?> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MediaEvent>> ListMatchableAsync(DateTimeOffset capturedAt, CancellationToken cancellationToken = default);
    Task UpsertAsync(MediaEvent mediaEvent, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
