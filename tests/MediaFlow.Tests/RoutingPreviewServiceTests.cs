using MediaFlow.Application.Abstractions;
using MediaFlow.Application.Services;
using MediaFlow.Core.Domain;
using MediaFlow.Infrastructure;
using MediaFlow.Infrastructure.Persistence;

namespace MediaFlow.Tests;

public sealed class RoutingPreviewServiceTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "mediaflow-routing", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task PreviewAsync_RotatesAcrossLargeSourceAndPersistsProgress()
    {
        var sourcePath = Directory.CreateDirectory(Path.Combine(root, "source")).FullName;
        var destinationPath = Directory.CreateDirectory(Path.Combine(root, "destination")).FullName;
        foreach (var name in new[] { "001.jpg", "002.jpg", "003.jpg" })
        {
            File.WriteAllText(Path.Combine(sourcePath, name), name);
        }

        var factory = new SqliteConnectionFactory(Path.Combine(root, "mediaflow.db"));
        await new SqliteDatabaseInitializer(factory).InitializeAsync();
        var mediaFiles = new SqliteMediaFileRepository(factory);
        var events = new SqliteMediaEventRepository(factory);
        var groups = new SqliteSourceGroupRepository(factory);
        var shares = new SqliteShareRepository(factory);
        var source = new Share
        {
            Name = "Phone",
            Path = sourcePath,
            Role = ShareRole.Source,
            StabilitySeconds = 0
        };
        var destination = new Share
        {
            Name = "Family",
            Path = destinationPath,
            Role = ShareRole.Destination
        };
        await shares.UpsertAsync(source);
        await shares.UpsertAsync(destination);
        var group = new SourceGroup { Name = "Parents", ShareIds = [source.Id] };
        await groups.UpsertAsync(group);
        await events.UpsertAsync(new MediaEvent
        {
            Name = "Vacation",
            StartAt = new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero),
            Status = MediaEventStatus.Active,
            SourceGroupId = group.Id,
            DestinationShareId = destination.Id
        });

        var clock = new MutableClock(new DateTimeOffset(2026, 8, 20, 12, 0, 0, TimeSpan.Zero));
        var selected = new List<string>();
        for (var cycle = 0; cycle < 3; cycle++)
        {
            var service = CreateService(factory, mediaFiles, events, groups, shares, clock);
            var item = Assert.Single(await service.PreviewAsync(source, 1));
            Assert.Equal(RoutingPreviewState.Matched, item.State);
            selected.Add(item.MediaFile.OriginalName);
            clock.UtcNow = clock.UtcNow.AddMinutes(1);
        }

        Assert.Equal(new[] { "001.jpg", "002.jpg", "003.jpg" }, selected);

        var restarted = CreateService(factory, mediaFiles, events, groups, shares, clock);
        var next = Assert.Single(await restarted.PreviewAsync(source, 1));
        Assert.Equal("001.jpg", next.MediaFile.OriginalName);
    }

    public void Dispose()
    {
        try { Directory.Delete(root, recursive: true); } catch { }
    }

    private static RoutingPreviewService CreateService(
        SqliteConnectionFactory factory,
        IMediaFileRepository mediaFiles,
        IMediaEventRepository events,
        ISourceGroupRepository groups,
        IShareRepository shares,
        IClock clock) => new(
            new ShareDiscoveryService(new LocalFileSystemGateway(), clock),
            new FixedMetadataExtractor(clock),
            mediaFiles,
            events,
            groups,
            shares,
            new DestinationPathResolver(),
            clock);

    private sealed class FixedMetadataExtractor(IClock clock) : IMediaMetadataExtractor
    {
        public Task<MediaMetadata> ExtractAsync(
            Share share,
            string path,
            MediaType mediaType,
            CancellationToken cancellationToken = default) => Task.FromResult(new MediaMetadata(
                clock.UtcNow,
                "DateTimeOriginal",
                false,
                null,
                null,
                null,
                null,
                null,
                "image/jpeg"));
    }

    private sealed class MutableClock(DateTimeOffset utcNow) : IClock
    {
        public DateTimeOffset UtcNow { get; set; } = utcNow;
    }
}
