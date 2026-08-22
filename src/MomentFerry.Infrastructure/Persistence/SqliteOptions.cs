namespace MomentFerry.Infrastructure.Persistence;

public sealed class SqliteOptions
{
    public const string SectionName = "MomentFerry:Database";
    public string Path { get; set; } = "data/momentferry.db";
}
