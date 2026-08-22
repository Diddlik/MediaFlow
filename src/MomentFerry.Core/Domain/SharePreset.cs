namespace MomentFerry.Core.Domain;

public sealed record SharePreset(
    string Id,
    string DisplayName,
    IReadOnlyList<string> IgnorePatterns,
    int StabilitySeconds = 30);

public static class SharePresets
{
    public static readonly SharePreset Generic = new(
        "generic",
        "Generic",
        Array.Empty<string>());

    public static readonly SharePreset Resilio = new(
        "resilio",
        "Resilio Sync",
        new[] { ".sync/**", "*.!sync" });

    public static readonly SharePreset Syncthing = new(
        "syncthing",
        "Syncthing",
        new[] { ".stfolder/**", ".stversions/**", "~syncthing~*" });

    public static readonly SharePreset Synology = new(
        "synology",
        "Synology NAS",
        new[] { "@eaDir/**", "#recycle/**" });

    public static IReadOnlyList<SharePreset> All { get; } =
        new[] { Generic, Resilio, Syncthing, Synology };
}
