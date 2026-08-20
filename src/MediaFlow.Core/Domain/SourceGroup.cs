namespace MediaFlow.Core.Domain;

public sealed class SourceGroup
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required string Name { get; init; }
    public IReadOnlyList<Guid> ShareIds { get; init; } = Array.Empty<Guid>();
}
