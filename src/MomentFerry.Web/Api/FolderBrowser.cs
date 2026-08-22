namespace MomentFerry.Web.Api;

public static class FolderBrowser
{
    public static IReadOnlyList<FolderNode> ListChildren(string path, IReadOnlyCollection<string> allowedRoots)
    {
        var fullPath = Path.GetFullPath(path);
        if (!IsSafePath(fullPath, allowedRoots) || !Directory.Exists(fullPath))
            throw new ArgumentException("Folder is outside the configured roots or does not exist.", nameof(path));

        return Directory.EnumerateDirectories(fullPath)
            .Select(directory => new DirectoryInfo(directory))
            .Where(directory => !directory.Attributes.HasFlag(FileAttributes.ReparsePoint))
            .OrderBy(directory => directory.Name, StringComparer.OrdinalIgnoreCase)
            .Select(directory => new FolderNode(
                directory.Name,
                directory.FullName,
                HasChildren(directory.FullName)))
            .ToArray();
    }

    public static bool IsSafePath(string path, IReadOnlyCollection<string> allowedRoots)
    {
        var fullPath = Path.GetFullPath(path);
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

        foreach (var configuredRoot in allowedRoots)
        {
            var root = Path.GetFullPath(configuredRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (!string.Equals(fullPath, root, comparison) &&
                !fullPath.StartsWith(root + Path.DirectorySeparatorChar, comparison))
                continue;

            var current = root;
            foreach (var segment in Path.GetRelativePath(root, fullPath)
                         .Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries))
            {
                current = Path.Combine(current, segment);
                if (Directory.Exists(current) && File.GetAttributes(current).HasFlag(FileAttributes.ReparsePoint))
                    return false;
            }

            return true;
        }

        return false;
    }

    private static bool HasChildren(string path)
    {
        try
        {
            return Directory.EnumerateDirectories(path)
                .Select(directory => new DirectoryInfo(directory))
                .Any(directory => !directory.Attributes.HasFlag(FileAttributes.ReparsePoint));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}

public sealed record FolderNode(string Name, string Path, bool HasChildren);
