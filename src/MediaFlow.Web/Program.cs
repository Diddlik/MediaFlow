using System.Text.Json.Serialization;
using MediaFlow.Application.Abstractions;
using MediaFlow.Core.Domain;
using MediaFlow.Infrastructure;
using MediaFlow.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddSingleton<IClock, SystemClock>();
builder.Services.AddSingleton<IFileSystemGateway, LocalFileSystemGateway>();
builder.Services.AddSingleton<IHashService, Sha256HashService>();

var databasePath = builder.Configuration["MediaFlow:Database:Path"] ?? "/app/data/mediaflow.db";
builder.Services.AddSingleton(new SqliteConnectionFactory(databasePath));
builder.Services.AddSingleton<IDatabaseInitializer, SqliteDatabaseInitializer>();
builder.Services.AddSingleton<IShareRepository, SqliteShareRepository>();

var app = builder.Build();

await app.Services.GetRequiredService<IDatabaseInitializer>().InitializeAsync();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/health", () => Results.Ok(new
{
    status = "ok",
    service = "MediaFlow"
}));

app.MapGet("/api/v1/info", (IClock clock) => Results.Ok(new
{
    name = "MediaFlow",
    status = "bootstrap",
    utcNow = clock.UtcNow,
    dryRun = builder.Configuration.GetValue("MediaFlow:DryRun", true)
}));

app.MapGet("/api/v1/share-presets", () => Results.Ok(SharePresets.All));

app.MapGet("/api/v1/shares", async (IShareRepository repository, CancellationToken ct) =>
    Results.Ok(await repository.ListAsync(ct)));

app.MapGet("/api/v1/shares/{id:guid}", async (Guid id, IShareRepository repository, CancellationToken ct) =>
{
    var share = await repository.GetAsync(id, ct);
    return share is null ? Results.NotFound() : Results.Ok(share);
});

app.MapPost("/api/v1/shares", async (ShareRequest request, IShareRepository repository, CancellationToken ct) =>
{
    var validation = Validate(request);
    if (validation is not null)
    {
        return validation;
    }

    var share = ToShare(Guid.NewGuid(), request);
    try
    {
        await repository.UpsertAsync(share, ct);
        return Results.Created($"/api/v1/shares/{share.Id}", share);
    }
    catch (Microsoft.Data.Sqlite.SqliteException ex) when (ex.SqliteErrorCode == 19)
    {
        return Results.Conflict(new { error = "A share with this path already exists." });
    }
});

app.MapPut("/api/v1/shares/{id:guid}", async (Guid id, ShareRequest request, IShareRepository repository, CancellationToken ct) =>
{
    if (await repository.GetAsync(id, ct) is null)
    {
        return Results.NotFound();
    }

    var validation = Validate(request);
    if (validation is not null)
    {
        return validation;
    }

    var share = ToShare(id, request);
    try
    {
        await repository.UpsertAsync(share, ct);
        return Results.Ok(share);
    }
    catch (Microsoft.Data.Sqlite.SqliteException ex) when (ex.SqliteErrorCode == 19)
    {
        return Results.Conflict(new { error = "A share with this path already exists." });
    }
});

app.MapDelete("/api/v1/shares/{id:guid}", async (Guid id, IShareRepository repository, CancellationToken ct) =>
    await repository.DeleteAsync(id, ct) ? Results.NoContent() : Results.NotFound());

app.Run();

static IResult? Validate(ShareRequest request)
{
    if (string.IsNullOrWhiteSpace(request.Name))
    {
        return Results.BadRequest(new { error = "Name is required." });
    }

    if (string.IsNullOrWhiteSpace(request.Path) || !Path.IsPathRooted(request.Path))
    {
        return Results.BadRequest(new { error = "Path must be an absolute path visible inside the container." });
    }

    if (request.StabilitySeconds is < 1 or > 3600)
    {
        return Results.BadRequest(new { error = "StabilitySeconds must be between 1 and 3600." });
    }

    if (request.AllowedMediaTypes is null || request.AllowedMediaTypes.Length == 0)
    {
        return Results.BadRequest(new { error = "At least one media type must be enabled." });
    }

    return null;
}

static Share ToShare(Guid id, ShareRequest request) => new()
{
    Id = id,
    Name = request.Name.Trim(),
    Path = request.Path.Trim(),
    Role = request.Role,
    Enabled = request.Enabled,
    Owner = string.IsNullOrWhiteSpace(request.Owner) ? null : request.Owner.Trim(),
    Group = string.IsNullOrWhiteSpace(request.Group) ? null : request.Group.Trim(),
    Preset = string.IsNullOrWhiteSpace(request.Preset) ? null : request.Preset.Trim(),
    StabilitySeconds = request.StabilitySeconds,
    Recursive = request.Recursive,
    DefaultTimeZone = string.IsNullOrWhiteSpace(request.DefaultTimeZone) ? null : request.DefaultTimeZone.Trim(),
    IgnorePatterns = request.IgnorePatterns?.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).Distinct().ToArray() ?? Array.Empty<string>(),
    AllowedMediaTypes = request.AllowedMediaTypes.ToHashSet()
};

public sealed record ShareRequest(
    string Name,
    string Path,
    ShareRole Role = ShareRole.Source,
    bool Enabled = true,
    string? Owner = null,
    string? Group = null,
    string? Preset = null,
    int StabilitySeconds = 30,
    bool Recursive = true,
    string? DefaultTimeZone = null,
    string[]? IgnorePatterns = null,
    MediaType[]? AllowedMediaTypes = null);
