namespace MediaFlow.Infrastructure.Persistence;

internal sealed record SqliteMigration(
    int Version,
    string Name,
    string Sql);
