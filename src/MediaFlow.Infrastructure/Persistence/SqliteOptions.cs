namespace MediaFlow.Infrastructure.Persistence;

public sealed class SqliteOptions
{
    public const string SectionName = "MediaFlow:Database";
    public string Path { get; set; } = "/app/data/mediaflow.db";
}
