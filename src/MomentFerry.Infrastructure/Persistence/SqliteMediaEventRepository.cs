using MomentFerry.Application.Abstractions;
using MomentFerry.Core.Domain;

namespace MomentFerry.Infrastructure.Persistence;

public sealed class SqliteMediaEventRepository(SqliteConnectionFactory connectionFactory) : IMediaEventRepository
{
    private const string SelectColumns = "id, name, type, start_at_utc, end_at_utc, status, source_group_id, destination_share_id, destination_folder_template, operation_mode, conflict_strategy, duplicate_strategy";

    public async Task<IReadOnlyList<MediaEvent>> ListAsync(CancellationToken cancellationToken = default)
    {
        var result = new List<MediaEvent>();
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT {SelectColumns} FROM events ORDER BY start_at_utc DESC";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(Read(reader));
        }
        return result;
    }

    public async Task<MediaEvent?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT {SelectColumns} FROM events WHERE id = $id";
        command.Parameters.AddWithValue("$id", id.ToString("D"));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? Read(reader) : null;
    }

    public async Task<IReadOnlyList<MediaEvent>> ListMatchableAsync(
        DateTimeOffset capturedAt,
        CancellationToken cancellationToken = default)
    {
        var result = new List<MediaEvent>();
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            SELECT {SelectColumns}
            FROM events
            WHERE status IN ($active, $closed)
              AND start_at_utc <= $captured
              AND (end_at_utc IS NULL OR end_at_utc >= $captured)
            ORDER BY start_at_utc DESC;
            """;
        command.Parameters.AddWithValue("$active", (int)MediaEventStatus.Active);
        command.Parameters.AddWithValue("$closed", (int)MediaEventStatus.Closed);
        command.Parameters.AddWithValue("$captured", capturedAt.UtcDateTime.ToString("O"));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(Read(reader));
        }
        return result;
    }

    public async Task UpsertAsync(MediaEvent mediaEvent, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow.ToString("O");
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO events (
                id, name, type, start_at_utc, end_at_utc, status,
                source_group_id, destination_share_id, destination_folder_template,
                operation_mode, conflict_strategy, duplicate_strategy,
                created_at_utc, updated_at_utc)
            VALUES (
                $id, $name, $type, $start, $end, $status,
                $groupId, $destinationId, $template,
                $operationMode, $conflictStrategy, $duplicateStrategy,
                $created, $updated)
            ON CONFLICT(id) DO UPDATE SET
                name = excluded.name,
                type = excluded.type,
                start_at_utc = excluded.start_at_utc,
                end_at_utc = excluded.end_at_utc,
                status = excluded.status,
                source_group_id = excluded.source_group_id,
                destination_share_id = excluded.destination_share_id,
                destination_folder_template = excluded.destination_folder_template,
                operation_mode = excluded.operation_mode,
                conflict_strategy = excluded.conflict_strategy,
                duplicate_strategy = excluded.duplicate_strategy,
                updated_at_utc = excluded.updated_at_utc;
            """;
        command.Parameters.AddWithValue("$id", mediaEvent.Id.ToString("D"));
        command.Parameters.AddWithValue("$name", mediaEvent.Name.Trim());
        command.Parameters.AddWithValue("$type", (object?)mediaEvent.Type ?? DBNull.Value);
        command.Parameters.AddWithValue("$start", mediaEvent.StartAt.UtcDateTime.ToString("O"));
        command.Parameters.AddWithValue("$end", mediaEvent.EndAt is null ? DBNull.Value : mediaEvent.EndAt.Value.UtcDateTime.ToString("O"));
        command.Parameters.AddWithValue("$status", (int)mediaEvent.Status);
        command.Parameters.AddWithValue("$groupId", mediaEvent.SourceGroupId.ToString("D"));
        command.Parameters.AddWithValue("$destinationId", mediaEvent.DestinationShareId.ToString("D"));
        command.Parameters.AddWithValue("$template", mediaEvent.DestinationFolderTemplate);
        command.Parameters.AddWithValue("$operationMode", (int)mediaEvent.OperationMode);
        command.Parameters.AddWithValue("$conflictStrategy", (int)mediaEvent.ConflictStrategy);
        command.Parameters.AddWithValue("$duplicateStrategy", (int)mediaEvent.DuplicateStrategy);
        command.Parameters.AddWithValue("$created", now);
        command.Parameters.AddWithValue("$updated", now);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM events WHERE id = $id";
        command.Parameters.AddWithValue("$id", id.ToString("D"));
        return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
    }

    private static MediaEvent Read(Microsoft.Data.Sqlite.SqliteDataReader reader) => new()
    {
        Id = Guid.Parse(reader.GetString(0)),
        Name = reader.GetString(1),
        Type = reader.IsDBNull(2) ? null : reader.GetString(2),
        StartAt = DateTimeOffset.Parse(reader.GetString(3)),
        EndAt = reader.IsDBNull(4) ? null : DateTimeOffset.Parse(reader.GetString(4)),
        Status = (MediaEventStatus)reader.GetInt32(5),
        SourceGroupId = Guid.Parse(reader.GetString(6)),
        DestinationShareId = Guid.Parse(reader.GetString(7)),
        DestinationFolderTemplate = reader.GetString(8),
        OperationMode = (OperationMode)reader.GetInt32(9),
        ConflictStrategy = (ConflictStrategy)reader.GetInt32(10),
        DuplicateStrategy = (DuplicateStrategy)reader.GetInt32(11)
    };
}
