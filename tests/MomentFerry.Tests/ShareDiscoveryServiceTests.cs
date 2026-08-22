using MomentFerry.Application.Abstractions;
using MomentFerry.Application.Services;
using MomentFerry.Core.Domain;
using MomentFerry.Infrastructure;

namespace MomentFerry.Tests;

public sealed class ShareDiscoveryServiceTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "momentferry-discovery", Guid.NewGuid().ToString("N"));

    [Fact]
    public void EnumerateSeesEveryFileWhileScanStopsAtTheLimit()
    {
        Directory.CreateDirectory(root);
        for (var index = 0; index < 12; index++)
        {
            File.WriteAllText(Path.Combine(root, $"shot-{index:00}.jpg"), "x");
        }

        var service = new ShareDiscoveryService(
            new LocalFileSystemGateway(),
            new FixedClock(DateTimeOffset.UnixEpoch));
        var share = new Share { Name = "pavel", Path = root, Role = ShareRole.Source };

        Assert.Equal(12, service.Enumerate(share).Count());
        Assert.Equal(5, service.Scan(share, 5).Count);
    }

    [Fact]
    public void Enumerate_IgnoresSynologyMetadataDirectories()
    {
        var album = Directory.CreateDirectory(Path.Combine(root, "album"));
        var metadata = Directory.CreateDirectory(Path.Combine(album.FullName, "@eaDir", "photo.jpg"));
        File.WriteAllText(Path.Combine(album.FullName, "photo.jpg"), "photo");
        File.WriteAllText(Path.Combine(metadata.FullName, "SYNOFILE_THUMB_M.jpg"), "thumbnail");

        var service = new ShareDiscoveryService(
            new LocalFileSystemGateway(),
            new FixedClock(DateTimeOffset.UnixEpoch));
        var share = new Share { Name = "pavel", Path = root, Role = ShareRole.Source };

        var file = Assert.Single(service.Enumerate(share));
        Assert.Equal("album/photo.jpg", file.RelativePath);
    }

    public void Dispose()
    {
        if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
    }

    private sealed class FixedClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset UtcNow => now;
    }
}
