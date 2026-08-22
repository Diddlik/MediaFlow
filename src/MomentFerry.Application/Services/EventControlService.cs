using MomentFerry.Application.Abstractions;
using MomentFerry.Core.Domain;

namespace MomentFerry.Application.Services;

public sealed class EventControlService(
    IMediaEventRepository events,
    ISourceGroupRepository sourceGroups,
    IShareRepository shares,
    IClock clock)
{
    public async Task<EventControlResult> StartAsync(
        Guid eventId,
        CancellationToken cancellationToken = default)
    {
        var existing = await events.GetAsync(eventId, cancellationToken);
        if (existing is null)
            return EventControlResult.NotFound("Event does not exist.");
        if (existing.Status != MediaEventStatus.Planned)
            return EventControlResult.Conflict("Only planned events can be started. Closed events keep their historical capture window.");

        var started = Copy(existing, clock.UtcNow, null, MediaEventStatus.Active);
        await events.UpsertAsync(started, cancellationToken);
        return EventControlResult.Success(started);
    }

    public async Task<EventControlResult> StopAsync(
        Guid eventId,
        CancellationToken cancellationToken = default)
    {
        var existing = await events.GetAsync(eventId, cancellationToken);
        if (existing is null)
            return EventControlResult.NotFound("Event does not exist.");
        if (existing.Status != MediaEventStatus.Active)
            return EventControlResult.Conflict("Only active events can be stopped.");

        var stopped = Copy(existing, existing.StartAt, clock.UtcNow, MediaEventStatus.Closed);
        await events.UpsertAsync(stopped, cancellationToken);
        return EventControlResult.Success(stopped);
    }

    public async Task<EventControlResult> QuickStartAsync(
        QuickStartEventCommand command,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(command.Name))
            return EventControlResult.Invalid("Name is required.");
        if (string.IsNullOrWhiteSpace(command.DestinationFolderTemplate) || Path.IsPathRooted(command.DestinationFolderTemplate))
            return EventControlResult.Invalid("DestinationFolderTemplate must be a relative folder template.");

        var sourceGroup = await sourceGroups.GetAsync(command.SourceGroupId, cancellationToken);
        if (sourceGroup is null)
            return EventControlResult.Invalid("Source group does not exist.");

        var destination = await shares.GetAsync(command.DestinationShareId, cancellationToken);
        if (destination is null || !destination.Enabled || destination.Role == ShareRole.Source)
            return EventControlResult.Invalid("Destination share must exist, be enabled, and support destination writes.");
        if (sourceGroup.ShareIds.Contains(command.DestinationShareId))
            return EventControlResult.Invalid("Destination share cannot also be a source of the same event. This prevents routing/sync loops.");

        var name = command.Name.Trim();
        var activeSameName = (await events.ListAsync(cancellationToken))
            .Where(x => x.Status == MediaEventStatus.Active &&
                        string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        if (activeSameName.Length == 1 &&
            activeSameName[0].SourceGroupId == command.SourceGroupId &&
            activeSameName[0].DestinationShareId == command.DestinationShareId)
        {
            return EventControlResult.Success(activeSameName[0]);
        }

        if (activeSameName.Length > 0)
            return EventControlResult.Conflict("An active event with this name already exists with different or ambiguous configuration.");

        var mediaEvent = new MediaEvent
        {
            Id = Guid.NewGuid(),
            Name = name,
            Type = string.IsNullOrWhiteSpace(command.Type) ? null : command.Type.Trim(),
            StartAt = clock.UtcNow,
            EndAt = null,
            Status = MediaEventStatus.Active,
            SourceGroupId = command.SourceGroupId,
            DestinationShareId = command.DestinationShareId,
            DestinationFolderTemplate = command.DestinationFolderTemplate.Trim(),
            OperationMode = command.OperationMode,
            ConflictStrategy = command.ConflictStrategy,
            DuplicateStrategy = command.DuplicateStrategy
        };
        await events.UpsertAsync(mediaEvent, cancellationToken);
        return EventControlResult.Created(mediaEvent);
    }

    public async Task<EventControlResult> QuickStopAsync(
        string name,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
            return EventControlResult.Invalid("Name is required.");

        var matches = (await events.ListAsync(cancellationToken))
            .Where(x => x.Status == MediaEventStatus.Active &&
                        string.Equals(x.Name, name.Trim(), StringComparison.OrdinalIgnoreCase))
            .ToArray();

        if (matches.Length == 0)
            return EventControlResult.NotFound("No active event with this name exists.");
        if (matches.Length > 1)
            return EventControlResult.Conflict("More than one active event has this name.");

        var stopped = Copy(matches[0], matches[0].StartAt, clock.UtcNow, MediaEventStatus.Closed);
        await events.UpsertAsync(stopped, cancellationToken);
        return EventControlResult.Success(stopped);
    }

    private static MediaEvent Copy(
        MediaEvent source,
        DateTimeOffset startAt,
        DateTimeOffset? endAt,
        MediaEventStatus status) => new()
    {
        Id = source.Id,
        Name = source.Name,
        Type = source.Type,
        StartAt = startAt,
        EndAt = endAt,
        Status = status,
        SourceGroupId = source.SourceGroupId,
        DestinationShareId = source.DestinationShareId,
        DestinationFolderTemplate = source.DestinationFolderTemplate,
        OperationMode = source.OperationMode,
        ConflictStrategy = source.ConflictStrategy,
        DuplicateStrategy = source.DuplicateStrategy
    };
}

public sealed record QuickStartEventCommand(
    string Name,
    Guid SourceGroupId,
    Guid DestinationShareId,
    string? Type = "Vacation",
    string DestinationFolderTemplate = "{event.name}",
    OperationMode OperationMode = OperationMode.SafeMove,
    ConflictStrategy ConflictStrategy = ConflictStrategy.AppendSourceName,
    DuplicateStrategy DuplicateStrategy = DuplicateStrategy.SafeMoveToExisting);

public enum EventControlStatus
{
    Success,
    Created,
    NotFound,
    Conflict,
    Invalid
}

public sealed record EventControlResult(
    EventControlStatus Status,
    MediaEvent? Event,
    string? Error)
{
    public static EventControlResult Success(MediaEvent mediaEvent) => new(EventControlStatus.Success, mediaEvent, null);
    public static EventControlResult Created(MediaEvent mediaEvent) => new(EventControlStatus.Created, mediaEvent, null);
    public static EventControlResult NotFound(string error) => new(EventControlStatus.NotFound, null, error);
    public static EventControlResult Conflict(string error) => new(EventControlStatus.Conflict, null, error);
    public static EventControlResult Invalid(string error) => new(EventControlStatus.Invalid, null, error);
}
