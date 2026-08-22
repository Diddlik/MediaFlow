namespace MomentFerry.Core.Domain;

public sealed class MediaEvent
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required string Name { get; init; }
    public string? Type { get; init; }
    public DateTimeOffset StartAt { get; init; }
    public DateTimeOffset? EndAt { get; init; }
    public MediaEventStatus Status { get; init; } = MediaEventStatus.Planned;
    public Guid SourceGroupId { get; init; }
    public Guid DestinationShareId { get; init; }
    public string DestinationFolderTemplate { get; init; } = "{event.name}";
    public OperationMode OperationMode { get; init; } = OperationMode.SafeMove;
    public ConflictStrategy ConflictStrategy { get; init; } = ConflictStrategy.AppendSourceName;
    public DuplicateStrategy DuplicateStrategy { get; init; } = DuplicateStrategy.SafeMoveToExisting;
}
