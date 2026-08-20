using MediaFlow.Application.Abstractions;
using MediaFlow.Core.Domain;
using MediaFlow.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<IClock, SystemClock>();
builder.Services.AddSingleton<IFileSystemGateway, LocalFileSystemGateway>();
builder.Services.AddSingleton<IHashService, Sha256HashService>();

var app = builder.Build();

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
    presets = SharePresets.All.Select(x => new
    {
        x.Id,
        x.DisplayName,
        x.StabilitySeconds,
        x.IgnorePatterns
    })
}));

app.Run();
