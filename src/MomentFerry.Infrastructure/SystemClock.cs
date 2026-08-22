using MomentFerry.Application.Abstractions;

namespace MomentFerry.Infrastructure;

public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
