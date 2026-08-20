using MediaFlow.Core.Domain;

namespace MediaFlow.Application.Abstractions;

public interface IShareRepository
{
    Task<IReadOnlyList<Share>> ListAsync(CancellationToken cancellationToken = default);
    Task<Share?> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task UpsertAsync(Share share, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}

public interface IDatabaseInitializer
{
    Task InitializeAsync(CancellationToken cancellationToken = default);
}
