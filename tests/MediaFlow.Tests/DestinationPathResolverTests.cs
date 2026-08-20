using MediaFlow.Application.Services;
using MediaFlow.Core.Domain;

namespace MediaFlow.Tests;

public sealed class DestinationPathResolverTests
{
    [Fact]
    public void Resolve_ExpandsTemplateInsideDestinationRoot()
    {
        var resolver = new DestinationPathResolver();
        var root = Path.Combine(Path.GetTempPath(), "mediaflow-tests", Guid.NewGuid().ToString("N"));
        var source = new Share
        {
            Name = "Phone A",
            Path = Path.Combine(root, "source"),
            Role = ShareRole.Source,
            Owner = "Pavel"
        };
        var destination = new Share
        {
            Name = "Family",
            Path = Path.Combine(root, "destination"),
            Role = ShareRole.Destination
        };
        var mediaEvent = new MediaEvent
        {
            Name = "Italy 2026",
            Type = "Vacation",
            StartAt = new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero),
            SourceGroupId = Guid.NewGuid(),
            DestinationShareId = destination.Id,
            DestinationFolderTemplate = "{year}/{event.name}/{owner}"
        };
        var media = new MediaFile
        {
            SourceShareId = source.Id,
            SourcePath = Path.Combine(source.Path, "IMG_0001.jpg"),
            OriginalName = "IMG_0001.jpg",
            Size = 123,
            Extension = ".jpg",
            MediaType = MediaType.Image,
            CapturedAt = new DateTimeOffset(2026, 8, 20, 12, 0, 0, TimeSpan.Zero),
            FirstSeenAt = DateTimeOffset.UtcNow,
            LastSeenAt = DateTimeOffset.UtcNow
        };

        var result = resolver.Resolve(mediaEvent, source, destination, media);

        Assert.Equal(
            Path.GetFullPath(Path.Combine(destination.Path, "2026", "Italy 2026", "Pavel", "IMG_0001.jpg")),
            result);
    }

    [Fact]
    public void EnsureInsideRoot_RejectsEscapingPath()
    {
        var root = Path.Combine(Path.GetTempPath(), "mediaflow-tests", Guid.NewGuid().ToString("N"), "destination");
        var escaped = Path.Combine(root, "..", "outside", "photo.jpg");

        Assert.Throws<InvalidOperationException>(() => DestinationPathResolver.EnsureInsideRoot(root, escaped));
    }

    [Theory]
    [InlineData(".")]
    [InlineData("..")]
    [InlineData("")]
    [InlineData("   ")]
    public void SafeSegment_DoesNotReturnUnsafeSpecialSegments(string value)
    {
        var result = DestinationPathResolver.SafeSegment(value);

        Assert.False(string.IsNullOrWhiteSpace(result));
        Assert.NotEqual(".", result);
        Assert.NotEqual("..", result);
    }
}
