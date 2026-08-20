namespace MediaFlow.Application.Services;

public sealed record StorageSafetyOptions(long MinimumFreeSpaceBytes)
{
    public const long DefaultMinimumFreeSpaceBytes = 512L * 1024L * 1024L;

    public static StorageSafetyOptions Default { get; } = new(DefaultMinimumFreeSpaceBytes);
}
