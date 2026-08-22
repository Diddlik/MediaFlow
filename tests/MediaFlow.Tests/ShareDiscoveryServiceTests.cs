using MediaFlow.Application.Abstractions;
using MediaFlow.Application.Services;
using MediaFlow.Core.Domain;
using MediaFlow.Infrastructure;

namespace MediaFlow.Tests;

public sealed class ShareDiscoveryServiceTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "mediaflow-discovery", Guid.NewGuid().ToString("N"));

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

    public void Dispose()
    {
        if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
    }

    private sealed class FixedClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset UtcNow => now;
    }
}
