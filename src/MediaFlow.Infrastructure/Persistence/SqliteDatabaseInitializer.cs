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

            CREATE TABLE IF NOT EXISTS source_groups (
                id TEXT PRIMARY KEY,
                name TEXT NOT NULL,
                created_at_utc TEXT NOT NULL,
                updated_at_utc TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS source_group_members (
                group_id TEXT NOT NULL,
                share_id TEXT NOT NULL,
                PRIMARY KEY (group_id, share_id),
                FOREIGN KEY (group_id) REFERENCES source_groups(id) ON DELETE CASCADE,
                FOREIGN KEY (share_id) REFERENCES shares(id) ON DELETE RESTRICT
            );

            CREATE TABLE IF NOT EXISTS events (
                id TEXT PRIMARY KEY,
                name TEXT NOT NULL,
                type TEXT NULL,
                start_at_utc TEXT NOT NULL,
                end_at_utc TEXT NULL,
                status INTEGER NOT NULL,
                source_group_id TEXT NOT NULL,
                destination_share_id TEXT NOT NULL,
                destination_folder_template TEXT NOT NULL,
                operation_mode INTEGER NOT NULL,
                conflict_strategy INTEGER NOT NULL,
                duplicate_strategy INTEGER NOT NULL,
                created_at_utc TEXT NOT NULL,
                updated_at_utc TEXT NOT NULL,
                FOREIGN KEY (source_group_id) REFERENCES source_groups(id) ON DELETE RESTRICT,
                FOREIGN KEY (destination_share_id) REFERENCES shares(id) ON DELETE RESTRICT
            );

            CREATE INDEX IF NOT EXISTS ix_events_window ON events(start_at_utc, end_at_utc, status);

            CREATE TABLE IF NOT EXISTS media_files (
                id TEXT PRIMARY KEY,
                source_share_id TEXT NOT NULL,
                source_path TEXT NOT NULL,
                original_name TEXT NOT NULL,
                size INTEGER NOT NULL,
                extension TEXT NOT NULL,
                media_type INTEGER NOT NULL,
                captured_at_utc TEXT NULL,
                timestamp_source TEXT NULL,
                timezone_inferred INTEGER NOT NULL,
                sha256 TEXT NULL,
                first_seen_at_utc TEXT NOT NULL,
                last_seen_at_utc TEXT NOT NULL,
                FOREIGN KEY (source_share_id) REFERENCES shares(id) ON DELETE CASCADE
            );

            CREATE UNIQUE INDEX IF NOT EXISTS ix_media_files_source ON media_files(source_share_id, source_path);
            CREATE INDEX IF NOT EXISTS ix_media_files_captured_at ON media_files(captured_at_utc);
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
