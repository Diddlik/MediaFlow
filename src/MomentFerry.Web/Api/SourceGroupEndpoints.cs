using MomentFerry.Application.Abstractions;
using MomentFerry.Core.Domain;

namespace MomentFerry.Web.Api;

public static class SourceGroupEndpoints
{
    public static IEndpointRouteBuilder MapSourceGroupEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/source-groups");

        group.MapGet("/", async (ISourceGroupRepository repository, CancellationToken ct) =>
            Results.Ok(await repository.ListAsync(ct)));

        group.MapGet("/{id:guid}", async (Guid id, ISourceGroupRepository repository, CancellationToken ct) =>
        {
            var sourceGroup = await repository.GetAsync(id, ct);
            return sourceGroup is null ? Results.NotFound() : Results.Ok(sourceGroup);
        });

        group.MapPost("/", async (
            SourceGroupRequest request,
            ISourceGroupRepository repository,
            IShareRepository shares,
            CancellationToken ct) =>
        {
            var validation = await ValidateAsync(request, shares, ct);
            if (validation is not null) return validation;

            var sourceGroup = ToDomain(Guid.NewGuid(), request);
            await repository.UpsertAsync(sourceGroup, ct);
            return Results.Created($"/api/v1/source-groups/{sourceGroup.Id}", sourceGroup);
        });

        group.MapPut("/{id:guid}", async (
            Guid id,
            SourceGroupRequest request,
            ISourceGroupRepository repository,
            IShareRepository shares,
            CancellationToken ct) =>
        {
            if (await repository.GetAsync(id, ct) is null) return Results.NotFound();
            var validation = await ValidateAsync(request, shares, ct);
            if (validation is not null) return validation;

            var sourceGroup = ToDomain(id, request);
            await repository.UpsertAsync(sourceGroup, ct);
            return Results.Ok(sourceGroup);
        });

        group.MapDelete("/{id:guid}", async (Guid id, ISourceGroupRepository repository, CancellationToken ct) =>
        {
            try
            {
                return await repository.DeleteAsync(id, ct) ? Results.NoContent() : Results.NotFound();
            }
            catch (Microsoft.Data.Sqlite.SqliteException ex) when (ex.SqliteErrorCode == 19)
            {
                return Results.Conflict(new { error = "Source group is still used by an event." });
            }
        });

        return app;
    }

    private static async Task<IResult?> ValidateAsync(
        SourceGroupRequest request,
        IShareRepository shares,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return Results.BadRequest(new { error = "Name is required." });
        }

        var ids = request.ShareIds?.Distinct().ToArray() ?? Array.Empty<Guid>();
        if (ids.Length == 0)
        {
            return Results.BadRequest(new { error = "At least one source share is required." });
        }

        foreach (var shareId in ids)
        {
            var share = await shares.GetAsync(shareId, cancellationToken);
            if (share is null)
            {
                return Results.BadRequest(new { error = $"Share {shareId} does not exist." });
            }
            if (!share.Enabled || share.Role == ShareRole.Destination)
            {
                return Results.BadRequest(new { error = $"Share '{share.Name}' is not an enabled source share." });
            }
        }

        return null;
    }

    private static SourceGroup ToDomain(Guid id, SourceGroupRequest request) => new()
    {
        Id = id,
        Name = request.Name.Trim(),
        ShareIds = request.ShareIds!.Distinct().ToArray()
    };
}

public sealed record SourceGroupRequest(string Name, Guid[]? ShareIds);
