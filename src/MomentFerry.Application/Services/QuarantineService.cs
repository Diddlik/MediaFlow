using MomentFerry.Application.Abstractions;
using MomentFerry.Core.Domain;

namespace MomentFerry.Application.Services;

public sealed class QuarantineService(IMediaOperationRepository operations, IClock clock)
{
    public async Task<MediaOperation> DismissAsync(
        Guid operationId,
        string? resolutionNote,
        CancellationToken cancellationToken = default)
    {
        var operation = await operations.GetAsync(operationId, cancellationToken)
            ?? throw new FileNotFoundException("Operation does not exist.");
        if (operation.State != MediaOperationState.Quarantined)
            throw new InvalidOperationException("Only quarantined operations can be dismissed.");

        var note = resolutionNote?.Trim() ?? string.Empty;
        if (note.Length is < 1 or > 500)
            throw new ArgumentException("Resolution note must contain between 1 and 500 characters.");

        var resolved = new MediaOperation
        {
            Id = operation.Id,
            MediaFileId = operation.MediaFileId,
            EventId = operation.EventId,
            State = MediaOperationState.Ignored,
            SourcePath = operation.SourcePath,
            StagingPath = operation.StagingPath,
            DestinationPath = operation.DestinationPath,
            SourceHash = operation.SourceHash,
            DestinationHash = operation.DestinationHash,
            RetryCount = operation.RetryCount,
            LastError = $"{operation.LastError} | Quarantine dismissed: {note}".TrimStart(' ', '|'),
            StartedAt = operation.StartedAt,
            CompletedAt = clock.UtcNow
        };
        await operations.UpsertAsync(resolved, cancellationToken);
        return resolved;
    }
}
