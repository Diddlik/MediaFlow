using MediaFlow.Web.Api;

namespace MediaFlow.Tests;

public sealed class FolderBrowserTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "mediaflow-tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void ListChildren_ReturnsOnlyImmediateFoldersWithExpansionState()
    {
        Directory.CreateDirectory(Path.Combine(root, "Pavel", "Camera"));
        Directory.CreateDirectory(Path.Combine(root, "Lena"));
        File.WriteAllText(Path.Combine(root, "ignored.txt"), "not a folder");

        var result = FolderBrowser.ListChildren(root, [root]);

        Assert.Collection(
            result,
            lena =>
            {
                Assert.Equal("Lena", lena.Name);
                Assert.False(lena.HasChildren);
            },
            pavel =>
            {
                Assert.Equal("Pavel", pavel.Name);
                Assert.True(pavel.HasChildren);
            });
    }

    [Fact]
    public void ListChildren_RejectsPathOutsideAllowedRoots()
    {
        Directory.CreateDirectory(root);
        var outside = Path.GetDirectoryName(root)!;

        Assert.Throws<ArgumentException>(() => FolderBrowser.ListChildren(outside, [root]));
    }

    public void Dispose()
    {
        try { Directory.Delete(root, recursive: true); } catch { }
    }
}
