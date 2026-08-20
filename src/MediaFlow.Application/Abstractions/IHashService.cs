namespace MediaFlow.Application.Abstractions;

public interface IHashService
{
    Task<string> ComputeSha256Async(Stream stream, CancellationToken cancellationToken = default);
}
