using System.Text.Json;

namespace MediaFlow.Web.Updates;

public interface IImageUpdateStatusStore
{
    Task<ImageUpdateStatus?> LoadAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(ImageUpdateStatus status, CancellationToken cancellationToken = default);
}

public sealed class JsonImageUpdateStatusStore(string path) : IImageUpdateStatusStore
{
    private readonly string path = Path.GetFullPath(path);
    private readonly SemaphoreSlim gate = new(1, 1);

    public async Task<ImageUpdateStatus?> LoadAsync(CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            if (!File.Exists(path)) return null;
            await using var stream = File.OpenRead(path);
            return await JsonSerializer.DeserializeAsync<ImageUpdateStatus>(stream, cancellationToken: cancellationToken);
        }
        catch (JsonException)
        {
            return null;
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task SaveAsync(ImageUpdateStatus status, CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
            var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                await using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    await JsonSerializer.SerializeAsync(stream, status, cancellationToken: cancellationToken);
                    await stream.FlushAsync(cancellationToken);
                }
                File.Move(temporary, path, overwrite: true);
            }
            finally
            {
                if (File.Exists(temporary)) File.Delete(temporary);
            }
        }
        finally
        {
            gate.Release();
        }
    }
}
