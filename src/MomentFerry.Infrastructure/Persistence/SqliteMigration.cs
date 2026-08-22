namespace MomentFerry.Infrastructure.Persistence;

internal sealed record SqliteMigration(
    int Version,
    string Name,
    string Sql);
