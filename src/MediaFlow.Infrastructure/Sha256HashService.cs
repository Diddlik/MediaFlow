using System.Security.Cryptography;
using MediaFlow.Application.Abstractions;

namespace MediaFlow.Infrastructure;

public sealed class Sha256HashService : IHashService
{
    public async Task<string> ComputeSha256Async(
        Stream stream,
        CancellationToken cancellationToken = default)
    {
        using var sha256 = SHA256.Create();
        var hash = await sha256.ComputeHashAsync(stream, cancellationToken);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
