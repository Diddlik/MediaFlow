using MomentFerry.Application.Abstractions;
using MomentFerry.Application.Services;
using MomentFerry.Core.Domain;

namespace MomentFerry.Web.Api;

public static class RoutingEndpoints
{
    public static IEndpointRouteBuilder MapRoutingEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/shares/{id:guid}/routing-preview", async (
            Guid id,
            int? limit,
            IShareRepository shares,
            RoutingPreviewService routing,
            IRuntimeSettingsStore runtimeSettings,
            CancellationToken ct) =>
        {
            var share = await shares.GetAsync(id, ct);
            if (share is null) return Results.NotFound();
            if (!share.Enabled || share.Role == ShareRole.Destination)
            {
                return Results.BadRequest(new { error = "Routing preview requires an enabled source share." });
            }

            try
            {
                var settings = await runtimeSettings.GetAsync(ct);
                var items = await routing.PreviewAsync(
                    share,
                    Math.Clamp(limit ?? 2000, 1, 2000),
                    ct,
                    settings.MaxParallelMetadataReads);
                return Results.Ok(new
                {
                    share.Id,
                    share.Name,
                    total = items.Count,
                    matched = items.Count(x => x.State == RoutingPreviewState.Matched),
                    unmatched = items.Count(x => x.State == RoutingPreviewState.Unmatched),
                    ambiguous = items.Count(x => x.State == RoutingPreviewState.Ambiguous),
                    items
                });
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or InvalidOperationException)
            {
                return Results.Problem(
                    title: "Routing preview failed",
                    detail: ex.Message,
                    statusCode: StatusCodes.Status503ServiceUnavailable);
            }
        });

        app.MapGet("/api/v1/media-files", async (
            int? limit,
            IMediaFileRepository repository,
            CancellationToken ct) =>
            Results.Ok(await repository.ListRecentAsync(Math.Clamp(limit ?? 200, 1, 2000), ct)));

        return app;
    }
}
