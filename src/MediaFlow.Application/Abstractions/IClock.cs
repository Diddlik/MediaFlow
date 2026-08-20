namespace MediaFlow.Application.Abstractions;

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}
