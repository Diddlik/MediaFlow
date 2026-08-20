using MediaFlow.Application.Abstractions;
using MediaFlow.Core.Domain;

namespace MediaFlow.Web.Api;

public static class EventEndpoints
{
    public static IEndpointRouteBuilder MapEventEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/events");

        group.MapGet("/", async (IMediaEventRepository repository, CancellationToken ct) =>
            Results.Ok(await repository.ListAsync(ct)));

        group.MapGet("/{id:guid}", async (Guid id, IMediaEventRepository repository, CancellationToken ct) =>
        {
            var mediaEvent = await repository.GetAsync(id, ct);
            return mediaEvent is null ? Results.NotFound() : Results.Ok(mediaEvent);
        });

        group.MapPost("/", async (
            EventRequest request,
            IMediaEventRepository repository,
            ISourceGroupRepository sourceGroups,
            IShareRepository shares,
            CancellationToken ct) =>
        {
            var validation = await ValidateAsync(request, sourceGroups, shares, ct);
            if (validation is not null) return validation;

            var mediaEvent = ToDomain(Guid.NewGuid(), request);
            await repository.UpsertAsync(mediaEvent, ct);
            return Results.Created($"/api/v1/events/{mediaEvent.Id}", mediaEvent);
        });

        group.MapPut("/{id:guid}", async (
            Guid id,
            EventRequest request,
            IMediaEventRepository repository,
            ISourceGroupRepository sourceGroups,
            IShareRepository shares,
            CancellationToken ct) =>
        {
            if (await repository.GetAsync(id, ct) is null) return Results.NotFound();
            var validation = await ValidateAsync(request, sourceGroups, shares, ct);
            if (validation is not null) return validation;

            var mediaEvent = ToDomain(id, request);
            await repository.UpsertAsync(mediaEvent, ct);
            return Results.Ok(mediaEvent);
        });

        group.MapPost("/{id:guid}/start", async (
            Guid id,
            IMediaEventRepository repository,
            IClock clock,
            CancellationToken ct) =>
        {
            var existing = await repository.GetAsync(id, ct);
            if (existing is null) return Results.NotFound();
            if (existing.Status != MediaEventStatus.Planned)
                return Results.Conflict(new { error = "Only planned events can be started. Closed events keep their historical capture window." });

            var started = Copy(existing, clock.UtcNow, null, MediaEventStatus.Active);
            await repository.UpsertAsync(started, ct);
            return Results.Ok(started);
        });

        group.MapPost("/{id:guid}/stop", async (
            Guid id,
            IMediaEventRepository repository,
            IClock clock,
            CancellationToken ct) =>
        {
            var existing = await repository.GetAsync(id, ct);
            if (existing is null) return Results.NotFound();
            if (existing.Status != MediaEventStatus.Active)
                return Results.Conflict(new { error = "Only active events can be stopped." });

            var stopped = Copy(existing, existing.StartAt, clock.UtcNow, MediaEventStatus.Closed);
            await repository.UpsertAsync(stopped, ct);
            return Results.Ok(stopped);
        });

        group.MapPost("/quick-start", async (
            QuickEventStartRequest request,
            IMediaEventRepository repository,
            ISourceGroupRepository sourceGroups,
            IShareRepository shares,
            IClock clock,
            CancellationToken ct) =>
        {
            var eventRequest = new EventRequest(
                request.Name,
                request.Type,
                clock.UtcNow,
                null,
                MediaEventStatus.Active,
                request.SourceGroupId,
                request.DestinationShareId,
                request.DestinationFolderTemplate,
                request.OperationMode,
                request.ConflictStrategy,
                request.DuplicateStrategy);

            var validation = await ValidateAsync(eventRequest, sourceGroups, shares, ct);
            if (validation is not null) return validation;

            var activeSameName = (await repository.ListAsync(ct))
                .Where(x => x.Status == MediaEventStatus.Active &&
                            string.Equals(x.Name, request.Name, StringComparison.OrdinalIgnoreCase))
                .ToArray();

            if (activeSameName.Length == 1 &&
                activeSameName[0].SourceGroupId == request.SourceGroupId &&
                activeSameName[0].DestinationShareId == request.DestinationShareId)
            {
                return Results.Ok(activeSameName[0]);
            }

            if (activeSameName.Length > 0)
                return Results.Conflict(new { error = "An active event with this name already exists with different or ambiguous configuration." });

            var mediaEvent = ToDomain(Guid.NewGuid(), eventRequest);
            await repository.UpsertAsync(mediaEvent, ct);
            return Results.Created($"/api/v1/events/{mediaEvent.Id}", mediaEvent);
        });

        group.MapPost("/quick-stop", async (
            QuickEventStopRequest request,
            IMediaEventRepository repository,
            IClock clock,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.Name))
                return Results.BadRequest(new { error = "Name is required." });

            var matches = (await repository.ListAsync(ct))
                .Where(x => x.Status == MediaEventStatus.Active &&
                            string.Equals(x.Name, request.Name.Trim(), StringComparison.OrdinalIgnoreCase))
                .ToArray();

            if (matches.Length == 0) return Results.NotFound(new { error = "No active event with this name exists." });
            if (matches.Length > 1) return Results.Conflict(new { error = "More than one active event has this name." });

            var stopped = Copy(matches[0], matches[0].StartAt, clock.UtcNow, MediaEventStatus.Closed);
            await repository.UpsertAsync(stopped, ct);
            return Results.Ok(stopped);
        });

        group.MapDelete("/{id:guid}", async (Guid id, IMediaEventRepository repository, CancellationToken ct) =>
            await repository.DeleteAsync(id, ct) ? Results.NoContent() : Results.NotFound());

        return app;
    }

    private static async Task<IResult?> ValidateAsync(
        EventRequest request,
        ISourceGroupRepository sourceGroups,
        IShareRepository shares,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return Results.BadRequest(new { error = "Name is required." });
        if (request.EndAt is not null && request.EndAt.Value < request.StartAt)
            return Results.BadRequest(new { error = "EndAt must not be before StartAt." });
        if (string.IsNullOrWhiteSpace(request.DestinationFolderTemplate) || Path.IsPathRooted(request.DestinationFolderTemplate))
            return Results.BadRequest(new { error = "DestinationFolderTemplate must be a relative folder template." });

        var sourceGroup = await sourceGroups.GetAsync(request.SourceGroupId, cancellationToken);
        if (sourceGroup is null)
            return Results.BadRequest(new { error = "Source group does not exist." });

        var destination = await shares.GetAsync(request.DestinationShareId, cancellationToken);
        if (destination is null || !destination.Enabled || destination.Role == ShareRole.Source)
            return Results.BadRequest(new { error = "Destination share must exist, be enabled, and support destination writes." });

        if (sourceGroup.ShareIds.Contains(request.DestinationShareId))
            return Results.BadRequest(new { error = "Destination share cannot also be a source of the same event. This prevents routing/sync loops." });

        return null;
    }

    private static MediaEvent ToDomain(Guid id, EventRequest request) => new()
    {
        Id = id,
        Name = request.Name.Trim(),
        Type = string.IsNullOrWhiteSpace(request.Type) ? null : request.Type.Trim(),
        StartAt = request.StartAt,
        EndAt = request.EndAt,
        Status = request.Status,
        SourceGroupId = request.SourceGroupId,
        DestinationShareId = request.DestinationShareId,
        DestinationFolderTemplate = request.DestinationFolderTemplate.Trim(),
        OperationMode = request.OperationMode,
        ConflictStrategy = request.ConflictStrategy,
        DuplicateStrategy = request.DuplicateStrategy
    };

    private static MediaEvent Copy(MediaEvent source, DateTimeOffset startAt, DateTimeOffset? endAt, MediaEventStatus status) => new()
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

public sealed record EventRequest(
    string Name,
    string? Type,
    DateTimeOffset StartAt,
    DateTimeOffset? EndAt,
    MediaEventStatus Status,
    Guid SourceGroupId,
    Guid DestinationShareId,
    string DestinationFolderTemplate = "{event.name}",
    OperationMode OperationMode = OperationMode.SafeMove,
    ConflictStrategy ConflictStrategy = ConflictStrategy.AppendSourceName,
    DuplicateStrategy DuplicateStrategy = DuplicateStrategy.SafeMoveToExisting);

public sealed record QuickEventStartRequest(
    string Name,
    Guid SourceGroupId,
    Guid DestinationShareId,
    string? Type = "Vacation",
    string DestinationFolderTemplate = "{event.name}",
    OperationMode OperationMode = OperationMode.SafeMove,
    ConflictStrategy ConflictStrategy = ConflictStrategy.AppendSourceName,
    DuplicateStrategy DuplicateStrategy = DuplicateStrategy.SafeMoveToExisting);

public sealed record QuickEventStopRequest(string Name);
