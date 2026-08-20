using MediaFlow.Core.Domain;
using MediaFlow.Infrastructure.Persistence;

namespace MediaFlow.Tests;

public sealed class SqliteMediaEventRepositoryTests
{
    [Fact]
    public async Task ListMatchableAsync_ClosedEventStillMatchesCaptureTimeInsideWindow()
    {
        var directory = Path.Combine(Path.GetTempPath(), "mediaflow-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var dbPath = Path.Combine(directory, "mediaflow.db");

        try
        {
            var factory = new SqliteConnectionFactory(dbPath);
            await new SqliteDatabaseInitializer(factory).InitializeAsync();

            var shares = new SqliteShareRepository(factory);
            var groups = new SqliteSourceGroupRepository(factory);
            var events = new SqliteMediaEventRepository(factory);

            var source = new Share
            {
                Name = "Phone",
                Path = Path.Combine(directory, "source"),
                Role = ShareRole.Source
            };
            var destination = new Share
            {
                Name = "Family",
                Path = Path.Combine(directory, "destination"),
                Role = ShareRole.Destination
            };
            await shares.UpsertAsync(source);
            await shares.UpsertAsync(destination);

            var sourceGroup = new SourceGroup
            {
                Name = "Family phones",
                ShareIds = [source.Id]
            };
            await groups.UpsertAsync(sourceGroup);

            var mediaEvent = new MediaEvent
            {
                Name = "Vacation",
                StartAt = new DateTimeOffset(2026, 8, 1, 8, 0, 0, TimeSpan.Zero),
                EndAt = new DateTimeOffset(2026, 8, 15, 20, 0, 0, TimeSpan.Zero),
                Status = MediaEventStatus.Closed,
                SourceGroupId = sourceGroup.Id,
                DestinationShareId = destination.Id
            };
            await events.UpsertAsync(mediaEvent);

            var inside = await events.ListMatchableAsync(
                new DateTimeOffset(2026, 8, 10, 12, 0, 0, TimeSpan.Zero));
            var after = await events.ListMatchableAsync(
                new DateTimeOffset(2026, 8, 16, 12, 0, 0, TimeSpan.Zero));

            Assert.Contains(inside, x => x.Id == mediaEvent.Id);
            Assert.DoesNotContain(after, x => x.Id == mediaEvent.Id);
        }
        finally
        {
            try { Directory.Delete(directory, recursive: true); } catch { }
        }
    }

    [Fact]
    public async Task ListMatchableAsync_PlannedEventDoesNotMatch()
    {
        var directory = Path.Combine(Path.GetTempPath(), "mediaflow-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var dbPath = Path.Combine(directory, "mediaflow.db");

        try
        {
            var factory = new SqliteConnectionFactory(dbPath);
            await new SqliteDatabaseInitializer(factory).InitializeAsync();

            var shares = new SqliteShareRepository(factory);
            var groups = new SqliteSourceGroupRepository(factory);
            var events = new SqliteMediaEventRepository(factory);

            var source = new Share { Name = "Phone", Path = Path.Combine(directory, "source"), Role = ShareRole.Source };
            var destination = new Share { Name = "Family", Path = Path.Combine(directory, "destination"), Role = ShareRole.Destination };
            await shares.UpsertAsync(source);
            await shares.UpsertAsync(destination);

            var sourceGroup = new SourceGroup { Name = "Phones", ShareIds = [source.Id] };
            await groups.UpsertAsync(sourceGroup);

            var planned = new MediaEvent
            {
                Name = "Future trip",
                StartAt = new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero),
                Status = MediaEventStatus.Planned,
                SourceGroupId = sourceGroup.Id,
                DestinationShareId = destination.Id
            };
            await events.UpsertAsync(planned);

            var matches = await events.ListMatchableAsync(
                new DateTimeOffset(2026, 8, 10, 12, 0, 0, TimeSpan.Zero));

            Assert.DoesNotContain(matches, x => x.Id == planned.Id);
        }
        finally
        {
            try { Directory.Delete(directory, recursive: true); } catch { }
        }
    }
}
