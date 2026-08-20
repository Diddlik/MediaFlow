using MediaFlow.Application.Abstractions;
using MediaFlow.Core.Domain;

namespace MediaFlow.Application.Services;

public sealed class OperationRetryService(
    IMediaOperationRepository operations,
    IMediaEventRepository events,
    IShareRepository shares,
    IFileSystemGateway fileSystem,
    SafeTransferService transfer,
    IClock clock)
{
    public async Task<TransferExecutionResult> RetryAsync(
        Guid operationId,
        CancellationToken cancellationToken = default)
    {
        var operation = await operations.GetAsync(operationId, cancellationToken)
            ?? throw new InvalidOperationException("Operation does not exist.");

        if (operation.State != MediaOperationState.RetryPending)
            throw new InvalidOperationException("Only RetryPending operations can be retried automatically.");
        if (operation.EventId is null)
            throw new InvalidOperationException("Operation has no event and cannot be retried.");

        var mediaEvent = await events.GetAsync(operation.EventId.Value, cancellationToken)
            ?? throw new InvalidOperationException("Operation event no longer exists.");
        var destinationShare = await shares.GetAsync(mediaEvent.DestinationShareId, cancellationToken)
            ?? throw new InvalidOperationException("Destination share no longer exists.");

        if (!string.IsNullOrWhiteSpace(operation.StagingPath) && fileSystem.FileExists(operation.StagingPath))
        {
            var stagingRoot = Path.GetFullPath(Path.Combine(destinationShare.Path, ".mediaflow-staging"))
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var stagingPath = Path.GetFullPath(operation.StagingPath);
            var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            if (!stagingPath.StartsWith(stagingRoot + Path.DirectorySeparatorChar, comparison))
                throw new InvalidOperationException("Persisted staging path is outside the destination staging directory.");

            fileSystem.DeleteFile(stagingPath);
        }

        var superseded = new MediaOperation
        {
            Id = operation.Id,
            MediaFileId = operation.MediaFileId,
            EventId = operation.EventId,
            State = MediaOperationState.Failed,
            SourcePath = operation.SourcePath,
            StagingPath = operation.StagingPath,
            DestinationPath = operation.DestinationPath,
            SourceHash = operation.SourceHash,
            DestinationHash = operation.DestinationHash,
            RetryCount = operation.RetryCount,
            LastError = "Superseded by explicit retry.",
            StartedAt = operation.StartedAt,
            CompletedAt = clock.UtcNow
        };
        await operations.UpsertAsync(superseded, cancellationToken);

        return await transfer.ExecuteAsync(operation.MediaFileId, operation.EventId.Value, cancellationToken);
    }
}
