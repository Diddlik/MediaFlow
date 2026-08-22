using MomentFerry.Application.Abstractions;
using MomentFerry.Application.Services;
using MomentFerry.Core.Domain;
using MomentFerry.Infrastructure.Persistence;

namespace MomentFerry.Tests;

public sealed class QuarantineServiceTests : IAsyncLifetime
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "momentferry-tests", Guid.NewGuid().ToString("N"));
    private SqliteMediaOperationRepository operations = null!;
    private MediaOperation quarantined = null!;

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(directory);
        var factory = new SqliteConnectionFactory(Path.Combine(directory, "momentferry.db"));
        await new SqliteDatabaseInitializer(factory).InitializeAsync();

        var share = new Share { Name = "Phone", Path = "/sources/phone", Role = ShareRole.Source };
        await new SqliteShareRepository(factory).UpsertAsync(share);
        var media = new MediaFile
        {
            SourceShareId = share.Id,
            SourcePath = "/sources/phone/photo.jpg",
            OriginalName = "photo.jpg",
            Size = 42,
            Extension = ".jpg",
            MediaType = MediaType.Image,
            FirstSeenAt = DateTimeOffset.UtcNow,
            LastSeenAt = DateTimeOffset.UtcNow
        };
        await new SqliteMediaFileRepository(factory).UpsertAsync(media);

        operations = new SqliteMediaOperationRepository(factory);
        quarantined = new MediaOperation
        {
            MediaFileId = media.Id,
            State = MediaOperationState.Quarantined,
            SourcePath = media.SourcePath,
            LastError = "Hash mismatch.",
            StartedAt = DateTimeOffset.UtcNow.AddMinutes(-1)
        };
        await operations.UpsertAsync(quarantined);
    }

    [Fact]
    public async Task Dismiss_PreservesAuditReasonAndRemovesItemFromQuarantine()
    {
        var completedAt = new DateTimeOffset(2026, 8, 21, 20, 0, 0, TimeSpan.Zero);
        var service = new QuarantineService(operations, new FixedClock(completedAt));

        var result = await service.DismissAsync(quarantined.Id, "Checked source manually");

        Assert.Equal(MediaOperationState.Ignored, result.State);
        Assert.Equal(completedAt, result.CompletedAt);
        Assert.Contains("Hash mismatch.", result.LastError);
        Assert.Contains("Checked source manually", result.LastError);
        Assert.Empty(await operations.ListByStateAsync(MediaOperationState.Quarantined));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    public async Task Dismiss_RejectsMissingResolutionNote(string? note)
    {
        var service = new QuarantineService(operations, new FixedClock(DateTimeOffset.UtcNow));

        await Assert.ThrowsAsync<ArgumentException>(() => service.DismissAsync(quarantined.Id, note));
        Assert.Single(await operations.ListByStateAsync(MediaOperationState.Quarantined));
    }

    public Task DisposeAsync()
    {
        try { Directory.Delete(directory, recursive: true); } catch { }
        return Task.CompletedTask;
    }

    private sealed class FixedClock(DateTimeOffset utcNow) : IClock
    {
        public DateTimeOffset UtcNow => utcNow;
    }
}
