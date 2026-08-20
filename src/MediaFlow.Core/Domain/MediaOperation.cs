namespace MediaFlow.Core.Domain;

public enum MediaOperationState
{
    Discovered,
    WaitingStable,
    MetadataPending,
    MetadataReady,
    RuleMatched,
    TransferPending,
    Copying,
    Verifying,
    DestinationCommitted,
    SourceFinalizePending,
    Completed,
    RetryPending,
    Quarantined,
    Ignored,
    Failed
}

public sealed class MediaOperation
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid MediaFileId { get; init; }
    public Guid? EventId { get; init; }
    public MediaOperationState State { get; init; } = MediaOperationState.Discovered;
    public required string SourcePath { get; init; }
    public string? StagingPath { get; init; }
    public string? DestinationPath { get; init; }
    public string? SourceHash { get; init; }
    public string? DestinationHash { get; init; }
    public int RetryCount { get; init; }
    public string? LastError { get; init; }
    public DateTimeOffset StartedAt { get; init; }
    public DateTimeOffset? CompletedAt { get; init; }
}
