using MediaFlow.Application.Abstractions;

namespace MediaFlow.Infrastructure.Persistence;

public sealed class SqliteDatabaseInitializer(SqliteConnectionFactory connectionFactory) : IDatabaseInitializer
{
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            PRAGMA journal_mode=WAL;
            PRAGMA foreign_keys=ON;

            CREATE TABLE IF NOT EXISTS shares (
                id TEXT PRIMARY KEY,
                name TEXT NOT NULL,
                path TEXT NOT NULL,
                role INTEGER NOT NULL,
                enabled INTEGER NOT NULL,
                owner TEXT NULL,
                group_name TEXT NULL,
                preset TEXT NULL,
                stability_seconds INTEGER NOT NULL,
                recursive INTEGER NOT NULL,
                default_timezone TEXT NULL,
                ignore_patterns_json TEXT NOT NULL,
                allowed_media_types_json TEXT NOT NULL,
                created_at_utc TEXT NOT NULL,
                updated_at_utc TEXT NOT NULL
            );

            CREATE UNIQUE INDEX IF NOT EXISTS ix_shares_path ON shares(path);
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
