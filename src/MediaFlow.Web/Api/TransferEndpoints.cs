using MediaFlow.Application.Abstractions;
using MediaFlow.Application.Services;

namespace MediaFlow.Web.Api;

public static class TransferEndpoints
{
    public static IEndpointRouteBuilder MapTransferEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/operations", async (
            int? limit,
            IMediaOperationRepository repository,
            CancellationToken ct) =>
            Results.Ok(await repository.ListRecentAsync(Math.Clamp(limit ?? 200, 1, 2000), ct)));

        app.MapPost("/api/v1/transfers", async (
            TransferRequest request,
            IConfiguration configuration,
            SafeTransferService transfer,
            CancellationToken ct) =>
        {
            if (configuration.GetValue("MediaFlow:DryRun", true))
            {
                return Results.Conflict(new
                {
                    error = "MediaFlow is in DryRun mode. Set MediaFlow:DryRun=false before executing transfers."
                });
            }

            try
            {
                var result = await transfer.ExecuteAsync(request.MediaFileId, request.EventId, ct);
                return Results.Ok(result);
            }
            catch (FileNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
            catch (Exception ex) when (ex is InvalidOperationException or IOException or NotSupportedException)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        app.MapPost("/api/v1/recovery", async (
            OperationRecoveryService recovery,
            CancellationToken ct) => Results.Ok(await recovery.RecoverAsync(ct)));

        return app;
    }
}

public sealed record TransferRequest(Guid MediaFileId, Guid EventId);
