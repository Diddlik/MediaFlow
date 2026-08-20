using MediaFlow.Core.Domain;

namespace MediaFlow.Application.Abstractions;

public interface ISourceGroupRepository
{
    Task<IReadOnlyList<SourceGroup>> ListAsync(CancellationToken cancellationToken = default);
    Task<SourceGroup?> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task UpsertAsync(SourceGroup sourceGroup, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
