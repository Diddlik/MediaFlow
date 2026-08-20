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
            if (IsDryRun(configuration))
                return DryRunConflict();

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

        app.MapPost("/api/v1/operations/{id:guid}/retry", async (
            Guid id,
            IConfiguration configuration,
            IMediaOperationRepository operations,
            IMediaEventRepository events,
            IShareRepository shares,
            IFileSystemGateway fileSystem,
            SafeTransferService transfer,
            IClock clock,
            CancellationToken ct) =>
        {
            if (IsDryRun(configuration))
                return DryRunConflict();

            try
            {
                var retry = new OperationRetryService(
                    operations,
                    events,
                    shares,
                    fileSystem,
                    transfer,
                    clock);
                return Results.Ok(await retry.RetryAsync(id, ct));
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

    private static bool IsDryRun(IConfiguration configuration) =>
        configuration.GetValue("MediaFlow:DryRun", true);

    private static IResult DryRunConflict() => Results.Conflict(new
    {
        error = "MediaFlow is in DryRun mode. Set MediaFlow:DryRun=false before executing or retrying transfers."
    });
}

public sealed record TransferRequest(Guid MediaFileId, Guid EventId);
