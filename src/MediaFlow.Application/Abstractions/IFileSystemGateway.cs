namespace MediaFlow.Application.Abstractions;

public interface IFileSystemGateway
{
    bool FileExists(string path);
    long GetFileLength(string path);
    DateTimeOffset GetLastWriteTimeUtc(string path);
    Stream OpenRead(string path);
    Task CopyFileAsync(string sourcePath, string destinationPath, CancellationToken cancellationToken = default);
    void EnsureDirectory(string path);
}
