using MediaFlow.Application.Abstractions;

namespace MediaFlow.Infrastructure;

public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
