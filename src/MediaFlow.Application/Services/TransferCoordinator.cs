using MediaFlow.Application.Abstractions;

namespace MediaFlow.Application.Services;

public sealed class TransferCoordinator(
    IMediaOperationRepository operations,
    SafeTransferService transfer)
{
    public async Task<CoordinatedTransferResult> ExecuteOnceAsync(
        Guid mediaFileId,
        Guid eventId,
        CancellationToken cancellationToken = default)
    {
        if (await operations.HasTerminalOperationAsync(mediaFileId, eventId, cancellationToken))
        {
            return new CoordinatedTransferResult(
                false,
                null,
                "This media file/event combination has already reached a terminal operation state.");
        }

        var result = await transfer.ExecuteAsync(mediaFileId, eventId, cancellationToken);
        return new CoordinatedTransferResult(true, result, result.Message);
    }
}

public sealed record CoordinatedTransferResult(
    bool Executed,
    TransferExecutionResult? Result,
    string? Message);
