using MediaFlow.Application.Abstractions;
using MediaFlow.Core.Domain;

namespace MediaFlow.Infrastructure.Persistence;

public sealed class SqliteSourceGroupRepository(SqliteConnectionFactory connectionFactory) : ISourceGroupRepository
{
    public async Task<IReadOnlyList<SourceGroup>> ListAsync(CancellationToken cancellationToken = default)
    {
        var result = new List<SourceGroup>();
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT id, name FROM source_groups ORDER BY name";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var id = Guid.Parse(reader.GetString(0));
            result.Add(new SourceGroup
            {
                Id = id,
                Name = reader.GetString(1),
                ShareIds = await GetMembersAsync(connectionFactory, id, cancellationToken)
            });
        }
        return result;
    }

    public async Task<SourceGroup?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT id, name FROM source_groups WHERE id = $id";
        command.Parameters.AddWithValue("$id", id.ToString("D"));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        var name = reader.GetString(1);
        await reader.DisposeAsync();
        return new SourceGroup
        {
            Id = id,
            Name = name,
            ShareIds = await GetMembersAsync(connectionFactory, id, cancellationToken)
        };
    }

    public async Task UpsertAsync(SourceGroup sourceGroup, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow.ToString("O");
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                INSERT INTO source_groups (id, name, created_at_utc, updated_at_utc)
                VALUES ($id, $name, $created, $updated)
                ON CONFLICT(id) DO UPDATE SET
                    name = excluded.name,
                    updated_at_utc = excluded.updated_at_utc;
                """;
            command.Parameters.AddWithValue("$id", sourceGroup.Id.ToString("D"));
            command.Parameters.AddWithValue("$name", sourceGroup.Name.Trim());
            command.Parameters.AddWithValue("$created", now);
            command.Parameters.AddWithValue("$updated", now);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var deleteMembers = connection.CreateCommand())
        {
            deleteMembers.Transaction = transaction;
            deleteMembers.CommandText = "DELETE FROM source_group_members WHERE group_id = $groupId";
            deleteMembers.Parameters.AddWithValue("$groupId", sourceGroup.Id.ToString("D"));
            await deleteMembers.ExecuteNonQueryAsync(cancellationToken);
        }

        foreach (var shareId in sourceGroup.ShareIds.Distinct())
        {
            await using var addMember = connection.CreateCommand();
            addMember.Transaction = transaction;
            addMember.CommandText = "INSERT INTO source_group_members (group_id, share_id) VALUES ($groupId, $shareId)";
            addMember.Parameters.AddWithValue("$groupId", sourceGroup.Id.ToString("D"));
            addMember.Parameters.AddWithValue("$shareId", shareId.ToString("D"));
            await addMember.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM source_groups WHERE id = $id";
        command.Parameters.AddWithValue("$id", id.ToString("D"));
        return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
    }

    private static async Task<IReadOnlyList<Guid>> GetMembersAsync(
        SqliteConnectionFactory connectionFactory,
        Guid groupId,
        CancellationToken cancellationToken)
    {
        var members = new List<Guid>();
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT share_id FROM source_group_members WHERE group_id = $groupId ORDER BY share_id";
        command.Parameters.AddWithValue("$groupId", groupId.ToString("D"));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            members.Add(Guid.Parse(reader.GetString(0)));
        }
        return members;
    }
}
