using MediaFlow.Application.Abstractions;

namespace MediaFlow.Infrastructure;

public sealed class LocalFileSystemGateway : IFileSystemGateway
{
    public bool FileExists(string path) => File.Exists(path);

    public long GetFileLength(string path) => new FileInfo(path).Length;

    public DateTimeOffset GetLastWriteTimeUtc(string path) => File.GetLastWriteTimeUtc(path);

    public Stream OpenRead(string path) => new FileStream(
        path,
        FileMode.Open,
        FileAccess.Read,
        FileShare.Read,
        bufferSize: 128 * 1024,
        options: FileOptions.Asynchronous | FileOptions.SequentialScan);

    public async Task CopyFileAsync(
        string sourcePath,
        string destinationPath,
        CancellationToken cancellationToken = default)
    {
        var destinationDirectory = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrWhiteSpace(destinationDirectory))
        {
            Directory.CreateDirectory(destinationDirectory);
        }

        await using var source = new FileStream(
            sourcePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 128 * 1024,
            options: FileOptions.Asynchronous | FileOptions.SequentialScan);

        await using var destination = new FileStream(
            destinationPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 128 * 1024,
            options: FileOptions.Asynchronous | FileOptions.WriteThrough);

        await source.CopyToAsync(destination, cancellationToken);
        await destination.FlushAsync(cancellationToken);
    }

    public void EnsureDirectory(string path) => Directory.CreateDirectory(path);
}
