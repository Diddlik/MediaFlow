using MomentFerry.Web.Updates;

namespace MomentFerry.Web.Api;

public static class UpdateEndpoints
{
    public static IEndpointRouteBuilder MapUpdateEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/updates", (ImageUpdateService updates, CancellationToken ct) => updates.GetStatusAsync(ct));
        app.MapPost("/api/v1/updates/check", (ImageUpdateService updates, CancellationToken ct) => updates.CheckAsync(ct));
        app.MapPost("/api/v1/updates/install", async (
            ImageUpdateRequest request,
            ImageUpdateService updates,
            CancellationToken ct) =>
        {
            if (!request.IsConfirmed)
                return Results.Conflict(new { error = $"Installing an update requires confirmation token '{ImageUpdateRequest.RequiredConfirmation}'." });
            try
            {
                return Results.Ok(await updates.InstallAsync(ct));
            }
            catch (Exception ex) when (ex is InvalidOperationException or HttpRequestException)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });
        return app;
    }
}

public sealed record ImageUpdateRequest(string? Confirmation)
{
    public const string RequiredConfirmation = "INSTALL_UPDATE";
    public bool IsConfirmed => string.Equals(Confirmation, RequiredConfirmation, StringComparison.Ordinal);
}
