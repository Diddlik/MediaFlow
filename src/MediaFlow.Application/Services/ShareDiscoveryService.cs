using System.Collections.Concurrent;
using System.IO.Enumeration;
using MediaFlow.Application.Abstractions;
using MediaFlow.Core.Domain;

namespace MediaFlow.Application.Services;

public sealed class ShareDiscoveryService(IFileSystemGateway fileSystem, IClock clock)
{
    private readonly ConcurrentDictionary<string, Observation> _observations = new(StringComparer.Ordinal);

    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".heic", ".heif", ".webp", ".gif", ".tif", ".tiff",
        ".dng", ".arw", ".cr2", ".cr3", ".nef", ".raf"
    };

    private static readonly HashSet<string> VideoExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp4", ".mov", ".m4v", ".avi", ".mkv", ".3gp", ".webm", ".mts", ".m2ts"
    };

    public IReadOnlyList<DiscoveredFile> Scan(Share share, int limit = 500)
    {
        if (!share.Enabled || share.Role == ShareRole.Destination || !fileSystem.DirectoryExists(share.Path))
        {
            return Array.Empty<DiscoveredFile>();
        }

        var now = clock.UtcNow;
        var result = new List<DiscoveredFile>();

        foreach (var path in fileSystem.EnumerateFiles(share.Path, share.Recursive))
        {
            if (result.Count >= limit)
            {
                break;
            }

            var relativePath = Path.GetRelativePath(share.Path, path).Replace('\\', '/');
            if (IsIgnored(relativePath, share.IgnorePatterns))
            {
                continue;
            }

            var mediaType = GetMediaType(path);
            if (mediaType == MediaType.Other || !share.AllowedMediaTypes.Contains(mediaType))
            {
                continue;
            }

            long size;
            DateTimeOffset lastWrite;
            try
            {
                size = fileSystem.GetFileLength(path);
                lastWrite = fileSystem.GetLastWriteTimeUtc(path);
            }
            catch (IOException)
            {
                continue;
            }

            var key = Path.GetFullPath(path);
            var observation = _observations.AddOrUpdate(
                key,
                _ => new Observation(size, lastWrite, now),
                (_, previous) => previous.Size == size && previous.LastWriteUtc == lastWrite
                    ? previous
                    : new Observation(size, lastWrite, now));

            var stable = now - observation.UnchangedSince >= TimeSpan.FromSeconds(share.StabilitySeconds);

            result.Add(new DiscoveredFile(
                path,
                relativePath,
                mediaType,
                size,
                lastWrite,
                observation.UnchangedSince,
                stable ? DiscoveryState.Stable : DiscoveryState.WaitingStable));
        }

        return result;
    }

    private static MediaType GetMediaType(string path)
    {
        var extension = Path.GetExtension(path);
        if (ImageExtensions.Contains(extension)) return MediaType.Image;
        if (VideoExtensions.Contains(extension)) return MediaType.Video;
        return MediaType.Other;
    }

    private static bool IsIgnored(string relativePath, IReadOnlyList<string> patterns)
    {
        if (relativePath.StartsWith(".mediaflow-staging/", StringComparison.Ordinal) ||
            string.Equals(relativePath, ".mediaflow-staging", StringComparison.Ordinal))
        {
            return true;
        }

        var fileName = Path.GetFileName(relativePath);
        foreach (var rawPattern in patterns)
        {
            var pattern = rawPattern.Trim().Replace('\\', '/');
            if (pattern.Length == 0) continue;

            if (pattern.EndsWith("/**", StringComparison.Ordinal))
            {
                var prefix = pattern[..^3].TrimEnd('/');
                if (relativePath.Equals(prefix, StringComparison.Ordinal) ||
                    relativePath.StartsWith(prefix + "/", StringComparison.Ordinal))
                {
                    return true;
                }
                continue;
            }

            if (FileSystemName.MatchesSimpleExpression(pattern, relativePath, ignoreCase: false) ||
                FileSystemName.MatchesSimpleExpression(pattern, fileName, ignoreCase: false))
            {
                return true;
            }
        }

        return false;
    }

    private sealed record Observation(long Size, DateTimeOffset LastWriteUtc, DateTimeOffset UnchangedSince);
}

public sealed record DiscoveredFile(
    string FullPath,
    string RelativePath,
    MediaType MediaType,
    long Size,
    DateTimeOffset LastWriteUtc,
    DateTimeOffset UnchangedSince,
    DiscoveryState State);

public enum DiscoveryState
{
    WaitingStable,
    Stable
}
