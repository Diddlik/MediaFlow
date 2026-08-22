namespace MomentFerry.Core.Domain;

public sealed class Share
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required string Name { get; init; }
    public required string Path { get; init; }
    public ShareRole Role { get; init; }
    public bool Enabled { get; init; } = true;
    public string? Owner { get; init; }
    public string? Group { get; init; }
    public string? Preset { get; init; }
    public int StabilitySeconds { get; init; } = 30;
    public bool Recursive { get; init; } = true;
    public string? DefaultTimeZone { get; init; }
    public IReadOnlyList<string> IgnorePatterns { get; init; } = Array.Empty<string>();
    public IReadOnlySet<MediaType> AllowedMediaTypes { get; init; } = new HashSet<MediaType>
    {
        MediaType.Image,
        MediaType.Video
    };
}
