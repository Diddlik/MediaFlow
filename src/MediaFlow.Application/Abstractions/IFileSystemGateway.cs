namespace MediaFlow.Application.Abstractions;

public interface IFileSystemGateway
{
    bool FileExists(string path);
    bool DirectoryExists(string path);
    IEnumerable<string> EnumerateFiles(string path, bool recursive);
    long GetFileLength(string path);
    DateTimeOffset GetLastWriteTimeUtc(string path);
    Stream OpenRead(string path);
    Task CopyFileAsync(string sourcePath, string destinationPath, CancellationToken cancellationToken = default);
    void MoveFile(string sourcePath, string destinationPath);
    void DeleteFile(string path);
    void EnsureDirectory(string path);
}
