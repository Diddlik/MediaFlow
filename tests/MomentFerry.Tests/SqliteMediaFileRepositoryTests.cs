using MomentFerry.Core.Domain;
using MomentFerry.Infrastructure.Persistence;

namespace MomentFerry.Tests;

public sealed class SqliteMediaFileRepositoryTests
{
    [Fact]
    public async Task RequeueByCaptureWindowAsync_OnlyResetsFilesInsideWindowAndSourceGroup()
    {
        var directory = Path.Combine(Path.GetTempPath(), "momentferry-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var dbPath = Path.Combine(directory, "momentferry.db");

        try
        {
            var factory = new SqliteConnectionFactory(dbPath);
            await new SqliteDatabaseInitializer(factory).InitializeAsync();

            var shares = new SqliteShareRepository(factory);
            var mediaFiles = new SqliteMediaFileRepository(factory);

            var watched = new Share { Name = "Phone", Path = Path.Combine(directory, "phone"), Role = ShareRole.Source };
            var other = new Share { Name = "Camera", Path = Path.Combine(directory, "camera"), Role = ShareRole.Source };
            await shares.UpsertAsync(watched);
            await shares.UpsertAsync(other);

            var lastSeen = new DateTimeOffset(2026, 8, 20, 12, 0, 0, TimeSpan.Zero);
            var inside = await AddAsync(mediaFiles, watched.Id, "inside.jpg", new DateTimeOffset(2026, 8, 10, 12, 0, 0, TimeSpan.Zero), lastSeen);
            var before = await AddAsync(mediaFiles, watched.Id, "before.jpg", new DateTimeOffset(2026, 7, 31, 12, 0, 0, TimeSpan.Zero), lastSeen);
            var after = await AddAsync(mediaFiles, watched.Id, "after.jpg", new DateTimeOffset(2026, 8, 16, 12, 0, 0, TimeSpan.Zero), lastSeen);
            var unrelated = await AddAsync(mediaFiles, other.Id, "unrelated.jpg", new DateTimeOffset(2026, 8, 10, 12, 0, 0, TimeSpan.Zero), lastSeen);

            var requeued = await mediaFiles.RequeueByCaptureWindowAsync(
                [watched.Id],
                new DateTimeOffset(2026, 8, 1, 8, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 8, 15, 20, 0, 0, TimeSpan.Zero));

            Assert.Equal(1, requeued);
            Assert.Equal(DateTimeOffset.MinValue, (await mediaFiles.GetAsync(inside))!.LastSeenAt);
            Assert.Equal(lastSeen, (await mediaFiles.GetAsync(before))!.LastSeenAt);
            Assert.Equal(lastSeen, (await mediaFiles.GetAsync(after))!.LastSeenAt);
            Assert.Equal(lastSeen, (await mediaFiles.GetAsync(unrelated))!.LastSeenAt);
        }
        finally
        {
            try { Directory.Delete(directory, recursive: true); } catch { }
        }
    }

    [Fact]
    public async Task RequeueByCaptureWindowAsync_OpenEndedWindowCoversEverythingAfterStart()
    {
        var directory = Path.Combine(Path.GetTempPath(), "momentferry-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var dbPath = Path.Combine(directory, "momentferry.db");

        try
        {
            var factory = new SqliteConnectionFactory(dbPath);
            await new SqliteDatabaseInitializer(factory).InitializeAsync();

            var shares = new SqliteShareRepository(factory);
            var mediaFiles = new SqliteMediaFileRepository(factory);

            var source = new Share { Name = "Phone", Path = Path.Combine(directory, "phone"), Role = ShareRole.Source };
            await shares.UpsertAsync(source);

            var lastSeen = new DateTimeOffset(2026, 8, 20, 12, 0, 0, TimeSpan.Zero);
            var late = await AddAsync(mediaFiles, source.Id, "late.jpg", new DateTimeOffset(2026, 9, 30, 12, 0, 0, TimeSpan.Zero), lastSeen);
            var early = await AddAsync(mediaFiles, source.Id, "early.jpg", new DateTimeOffset(2026, 7, 1, 12, 0, 0, TimeSpan.Zero), lastSeen);

            var requeued = await mediaFiles.RequeueByCaptureWindowAsync(
                [source.Id],
                new DateTimeOffset(2026, 8, 1, 8, 0, 0, TimeSpan.Zero),
                null);

            Assert.Equal(1, requeued);
            Assert.Equal(DateTimeOffset.MinValue, (await mediaFiles.GetAsync(late))!.LastSeenAt);
            Assert.Equal(lastSeen, (await mediaFiles.GetAsync(early))!.LastSeenAt);
        }
        finally
        {
            try { Directory.Delete(directory, recursive: true); } catch { }
        }
    }

    private static async Task<Guid> AddAsync(
        SqliteMediaFileRepository repository,
        Guid sourceShareId,
        string name,
        DateTimeOffset capturedAt,
        DateTimeOffset lastSeenAt)
    {
        var mediaFile = new MediaFile
        {
            Id = Guid.NewGuid(),
            SourceShareId = sourceShareId,
            SourcePath = $"/shares/{sourceShareId:N}/{name}",
            OriginalName = name,
            Size = 1024,
            Extension = ".jpg",
            MediaType = MediaType.Image,
            CapturedAt = capturedAt,
            TimestampSource = "Exif",
            FirstSeenAt = lastSeenAt,
            LastSeenAt = lastSeenAt
        };
        await repository.UpsertAsync(mediaFile);
        return mediaFile.Id;
    }
}
