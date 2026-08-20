using MediaFlow.Application.Abstractions;
using MediaFlow.Core.Domain;

namespace MediaFlow.Infrastructure.Persistence;

public sealed class SqliteMediaFileRepository(SqliteConnectionFactory connectionFactory) : IMediaFileRepository
{
    private const string SelectColumns = "id, source_share_id, source_path, original_name, size, extension, media_type, captured_at_utc, timestamp_source, timezone_inferred, sha256, first_seen_at_utc, last_seen_at_utc";

    public async Task<IReadOnlyList<MediaFile>> ListRecentAsync(int limit = 200, CancellationToken cancellationToken = default)
    {
        var result = new List<MediaFile>();
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT {SelectColumns} FROM media_files ORDER BY last_seen_at_utc DESC LIMIT $limit";
        command.Parameters.AddWithValue("$limit", Math.Clamp(limit, 1, 5000));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(Read(reader));
        }
        return result;
    }

    public async Task<MediaFile?> GetBySourceAsync(
        Guid sourceShareId,
        string sourcePath,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT {SelectColumns} FROM media_files WHERE source_share_id = $shareId AND source_path = $path";
        command.Parameters.AddWithValue("$shareId", sourceShareId.ToString("D"));
        command.Parameters.AddWithValue("$path", sourcePath);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? Read(reader) : null;
    }

    public async Task UpsertAsync(MediaFile mediaFile, CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO media_files (
                id, source_share_id, source_path, original_name, size, extension, media_type,
                captured_at_utc, timestamp_source, timezone_inferred, sha256,
                first_seen_at_utc, last_seen_at_utc)
            VALUES (
                $id, $sourceShareId, $sourcePath, $originalName, $size, $extension, $mediaType,
                $capturedAt, $timestampSource, $timezoneInferred, $sha256,
                $firstSeen, $lastSeen)
            ON CONFLICT(source_share_id, source_path) DO UPDATE SET
                original_name = excluded.original_name,
                size = excluded.size,
                extension = excluded.extension,
                media_type = excluded.media_type,
                captured_at_utc = excluded.captured_at_utc,
                timestamp_source = excluded.timestamp_source,
                timezone_inferred = excluded.timezone_inferred,
                sha256 = COALESCE(excluded.sha256, media_files.sha256),
                last_seen_at_utc = excluded.last_seen_at_utc;
            """;
        command.Parameters.AddWithValue("$id", mediaFile.Id.ToString("D"));
        command.Parameters.AddWithValue("$sourceShareId", mediaFile.SourceShareId.ToString("D"));
        command.Parameters.AddWithValue("$sourcePath", mediaFile.SourcePath);
        command.Parameters.AddWithValue("$originalName", mediaFile.OriginalName);
        command.Parameters.AddWithValue("$size", mediaFile.Size);
        command.Parameters.AddWithValue("$extension", mediaFile.Extension);
        command.Parameters.AddWithValue("$mediaType", (int)mediaFile.MediaType);
        command.Parameters.AddWithValue("$capturedAt", mediaFile.CapturedAt is null ? DBNull.Value : mediaFile.CapturedAt.Value.UtcDateTime.ToString("O"));
        command.Parameters.AddWithValue("$timestampSource", (object?)mediaFile.TimestampSource ?? DBNull.Value);
        command.Parameters.AddWithValue("$timezoneInferred", mediaFile.IsTimezoneInferred ? 1 : 0);
        command.Parameters.AddWithValue("$sha256", (object?)mediaFile.Sha256 ?? DBNull.Value);
        command.Parameters.AddWithValue("$firstSeen", mediaFile.FirstSeenAt.UtcDateTime.ToString("O"));
        command.Parameters.AddWithValue("$lastSeen", mediaFile.LastSeenAt.UtcDateTime.ToString("O"));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static MediaFile Read(Microsoft.Data.Sqlite.SqliteDataReader reader) => new()
    {
        Id = Guid.Parse(reader.GetString(0)),
        SourceShareId = Guid.Parse(reader.GetString(1)),
        SourcePath = reader.GetString(2),
        OriginalName = reader.GetString(3),
        Size = reader.GetInt64(4),
        Extension = reader.GetString(5),
        MediaType = (MediaType)reader.GetInt32(6),
        CapturedAt = reader.IsDBNull(7) ? null : DateTimeOffset.Parse(reader.GetString(7)),
        TimestampSource = reader.IsDBNull(8) ? null : reader.GetString(8),
        IsTimezoneInferred = reader.GetInt32(9) != 0,
        Sha256 = reader.IsDBNull(10) ? null : reader.GetString(10),
        FirstSeenAt = DateTimeOffset.Parse(reader.GetString(11)),
        LastSeenAt = DateTimeOffset.Parse(reader.GetString(12))
    };
}
