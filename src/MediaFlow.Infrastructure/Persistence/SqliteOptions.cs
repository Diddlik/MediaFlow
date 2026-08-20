namespace MediaFlow.Infrastructure.Persistence;

public sealed class SqliteOptions
{
    public const string SectionName = "MediaFlow:Database";
    public string Path { get; set; } = "data/mediaflow.db";
}
