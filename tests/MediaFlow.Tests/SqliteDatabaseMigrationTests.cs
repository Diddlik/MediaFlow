using MediaFlow.Infrastructure.Persistence;

namespace MediaFlow.Tests;

public sealed class SqliteDatabaseMigrationTests : IAsyncLifetime
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "mediaflow-migrations", Guid.NewGuid().ToString("N"));
    private string DatabasePath => Path.Combine(_root, "mediaflow.db");

    public Task InitializeAsync()
    {
        Directory.CreateDirectory(_root);
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        try { Directory.Delete(_root, recursive: true); } catch { }
        return Task.CompletedTask;
    }

    [Fact]
    public async Task FreshDatabase_AppliesCurrentSchemaOnceAndIsIdempotent()
    {
        var factory = new SqliteConnectionFactory(DatabasePath);
        var initializer = new SqliteDatabaseInitializer(factory);

        await initializer.InitializeAsync();
        await initializer.InitializeAsync();

        await using var connection = await factory.OpenAsync();
        await using var versionCommand = connection.CreateCommand();
        versionCommand.CommandText = "SELECT version, name FROM schema_migrations ORDER BY version;";
        await using var reader = await versionCommand.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.Equal(SqliteDatabaseInitializer.CurrentSchemaVersion, reader.GetInt32(0));
        Assert.Equal("initial-schema", reader.GetString(1));
        Assert.False(await reader.ReadAsync());
        await reader.DisposeAsync();

        foreach (var table in new[] { "shares", "source_groups", "events", "media_files", "operations" })
        {
            await using var tableCommand = connection.CreateCommand();
            tableCommand.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name=$name;";
            tableCommand.Parameters.AddWithValue("$name", table);
            Assert.Equal(1L, (long)(await tableCommand.ExecuteScalarAsync())!);
        }
    }

    [Fact]
    public async Task LegacyDatabase_IsBaselinedWithoutLosingExistingShareData()
    {
        var factory = new SqliteConnectionFactory(DatabasePath);
        var shareId = Guid.NewGuid().ToString("D");

        await using (var connection = await factory.OpenAsync())
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                CREATE TABLE shares (
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
                CREATE UNIQUE INDEX ix_shares_path ON shares(path);
                INSERT INTO shares (
                    id, name, path, role, enabled, owner, group_name, preset,
                    stability_seconds, recursive, default_timezone,
                    ignore_patterns_json, allowed_media_types_json,
                    created_at_utc, updated_at_utc)
                VALUES (
                    $id, 'Legacy phone', '/sources/legacy', 0, 1, NULL, NULL, NULL,
                    30, 1, 'Europe/Berlin', '[]', '[0,1]',
                    '2026-08-01T00:00:00.0000000+00:00',
                    '2026-08-01T00:00:00.0000000+00:00');
                """;
            command.Parameters.AddWithValue("$id", shareId);
            await command.ExecuteNonQueryAsync();
        }

        await new SqliteDatabaseInitializer(factory).InitializeAsync();

        await using var verify = await factory.OpenAsync();
        await using var shareCommand = verify.CreateCommand();
        shareCommand.CommandText = "SELECT name, path FROM shares WHERE id=$id;";
        shareCommand.Parameters.AddWithValue("$id", shareId);
        await using var shareReader = await shareCommand.ExecuteReaderAsync();
        Assert.True(await shareReader.ReadAsync());
        Assert.Equal("Legacy phone", shareReader.GetString(0));
        Assert.Equal("/sources/legacy", shareReader.GetString(1));
        await shareReader.DisposeAsync();

        await using var migrationCommand = verify.CreateCommand();
        migrationCommand.CommandText = "SELECT COUNT(*) FROM schema_migrations WHERE version=$version;";
        migrationCommand.Parameters.AddWithValue("$version", SqliteDatabaseInitializer.CurrentSchemaVersion);
        Assert.Equal(1L, (long)(await migrationCommand.ExecuteScalarAsync())!);

        await using var eventsCommand = verify.CreateCommand();
        eventsCommand.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='events';";
        Assert.Equal(1L, (long)(await eventsCommand.ExecuteScalarAsync())!);
    }

    [Fact]
    public async Task DatabaseFromNewerApplication_IsRejected()
    {
        var factory = new SqliteConnectionFactory(DatabasePath);
        await using (var connection = await factory.OpenAsync())
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                CREATE TABLE schema_migrations (
                    version INTEGER PRIMARY KEY,
                    name TEXT NOT NULL,
                    applied_at_utc TEXT NOT NULL
                );
                INSERT INTO schema_migrations (version, name, applied_at_utc)
                VALUES (999, 'future-schema', '2026-08-20T00:00:00.0000000+00:00');
                """;
            await command.ExecuteNonQueryAsync();
        }

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new SqliteDatabaseInitializer(factory).InitializeAsync());

        Assert.Contains("newer", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("999", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EveryConnection_EnablesForeignKeysAndBusyTimeout()
    {
        var factory = new SqliteConnectionFactory(DatabasePath);
        await new SqliteDatabaseInitializer(factory).InitializeAsync();

        await using var connection = await factory.OpenAsync();
        await using var foreignKeys = connection.CreateCommand();
        foreignKeys.CommandText = "PRAGMA foreign_keys;";
        Assert.Equal(1L, Convert.ToInt64(await foreignKeys.ExecuteScalarAsync()));

        await using var busyTimeout = connection.CreateCommand();
        busyTimeout.CommandText = "PRAGMA busy_timeout;";
        Assert.Equal(5000L, Convert.ToInt64(await busyTimeout.ExecuteScalarAsync()));
    }
}
