using System.Net;
using MediaFlow.Application.Abstractions;
using MediaFlow.Web.Updates;
using MediaFlow.Web.Api;

namespace MediaFlow.Tests;

public sealed class ImageUpdateServiceTests
{
    [Fact]
    public async Task Check_ExposesNewVersionAndChangelog()
    {
        var handler = new QueueHandler(JsonResponse("""
            {"tag_name":"v1.2.0","body":"Important fixes","published_at":"2026-08-21T18:00:00Z","html_url":"https://example.test/release"}
            """));
        var service = CreateService(handler);

        var status = await service.CheckAsync();

        Assert.True(status.UpdateAvailable);
        Assert.Equal("1.2.0", status.LatestVersion);
        Assert.Equal("Important fixes", status.Changelog);
    }

    [Fact]
    public async Task Check_StableReleaseIsNewerThanMatchingPrerelease()
    {
        var handler = new QueueHandler(JsonResponse("""{"tag_name":"v1.0.0","body":"Stable"}"""));
        var service = CreateService(handler, runningVersion: "1.0.0-beta.2");

        var status = await service.CheckAsync();

        Assert.True(status.UpdateAvailable);
    }

    [Fact]
    public async Task Install_UsesAuthenticatedUpdaterCompanion()
    {
        var handler = new QueueHandler(
            JsonResponse("""{"tag_name":"v1.2.0","body":"Fixes","published_at":"2026-08-21T18:00:00Z"}"""),
            new HttpResponseMessage(HttpStatusCode.OK));
        var service = CreateService(handler);

        var status = await service.InstallAsync();

        Assert.NotNull(status.LastUpdateRequestedAt);
        Assert.Equal("http://updater:8080/v1/update", handler.Requests[1].Uri);
        Assert.Equal("Bearer secret-token", handler.Requests[1].Authorization);
    }

    [Fact]
    public async Task Check_NetworkFailureIsExposedAsStatus()
    {
        var service = CreateService(new ThrowingHandler());

        var status = await service.CheckAsync();

        Assert.False(status.UpdateAvailable);
        Assert.Contains("release service unavailable", status.LastError);
    }

    [Fact]
    public async Task Install_UpdaterFailureIsPersistedAndReported()
    {
        var statusStore = new MemoryStatusStore();
        var handler = new QueueHandler(
            JsonResponse("""{"tag_name":"v1.2.0","body":"Fixes"}"""),
            new HttpResponseMessage(HttpStatusCode.InternalServerError));
        var service = CreateService(handler, statusStore);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => service.InstallAsync());
        var status = await statusStore.LoadAsync();

        Assert.Contains("Updater request failed", error.Message);
        Assert.Contains("500", status!.LastError);
        Assert.NotNull(status.LastUpdateRequestedAt);
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("install_update", false)]
    [InlineData("INSTALL_UPDATE", true)]
    public void InstallConfirmation_IsExact(string? confirmation, bool expected) =>
        Assert.Equal(expected, new ImageUpdateRequest(confirmation).IsConfirmed);

    [Fact]
    public async Task JsonStatusStore_PersistsLatestCheckAcrossInstances()
    {
        var directory = Path.Combine(Path.GetTempPath(), "mediaflow-tests", Guid.NewGuid().ToString("N"));
        var path = Path.Combine(directory, "update-status.json");
        try
        {
            var expected = await CreateService(new QueueHandler(JsonResponse(
                """{"tag_name":"v1.2.0","body":"Persist me","published_at":"2026-08-21T18:00:00Z"}""")),
                new JsonImageUpdateStatusStore(path)).CheckAsync();

            var actual = await new JsonImageUpdateStatusStore(path).LoadAsync();

            Assert.Equal(expected, actual);
        }
        finally
        {
            try { Directory.Delete(directory, recursive: true); } catch { }
        }
    }

    [Fact]
    public async Task GetStatus_MarksRequestedVersionCompletedAfterHealthyRestart()
    {
        var statusStore = new MemoryStatusStore();
        await statusStore.SaveAsync(new ImageUpdateStatus(
            "1.0.0", "1.2.0", true, "Fixes", null, null, false, true,
            DateTimeOffset.UtcNow.AddMinutes(-2), DateTimeOffset.UtcNow.AddMinutes(-1), null, null));
        var service = CreateService(new QueueHandler(), statusStore, runningVersion: "1.2.0");

        var status = await service.GetStatusAsync();

        Assert.Equal("1.2.0", status.RunningVersion);
        Assert.False(status.UpdateAvailable);
        Assert.Equal(new DateTimeOffset(2026, 8, 21, 20, 0, 0, TimeSpan.Zero), status.LastUpdateCompletedAt);
        Assert.Null(status.LastError);
    }

    private static ImageUpdateService CreateService(
        HttpMessageHandler handler,
        IImageUpdateStatusStore? statusStore = null,
        string runningVersion = "1.0.0") => new(
        new HttpClient(handler),
        new ImageUpdateOptions(
            "https://api.example.test/releases/latest",
            "http://updater:8080/",
            "secret-token",
            runningVersion),
        new MemorySettingsStore(),
        statusStore ?? new MemoryStatusStore(),
        new FixedClock());

    private static HttpResponseMessage JsonResponse(string json) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
    };

    private sealed class QueueHandler(params HttpResponseMessage[] responses) : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> responses = new(responses);
        public List<(string Uri, string? Authorization)> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add((request.RequestUri!.ToString(), request.Headers.Authorization?.ToString()));
            return Task.FromResult(responses.Dequeue());
        }
    }

    private sealed class ThrowingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new HttpRequestException("release service unavailable");
    }

    private sealed class MemorySettingsStore : IRuntimeSettingsStore
    {
        private MediaFlowRuntimeSettings settings = new();
        public Task<MediaFlowRuntimeSettings> GetAsync(CancellationToken cancellationToken = default) => Task.FromResult(settings);
        public Task<MediaFlowRuntimeSettings> UpdateAsync(MediaFlowRuntimeSettings value, CancellationToken cancellationToken = default) =>
            Task.FromResult(settings = value);
        public Task<MediaFlowRuntimeSettings> ResetAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(settings = new());
    }

    private sealed class MemoryStatusStore : IImageUpdateStatusStore
    {
        private ImageUpdateStatus? status;
        public Task<ImageUpdateStatus?> LoadAsync(CancellationToken cancellationToken = default) => Task.FromResult(status);
        public Task SaveAsync(ImageUpdateStatus value, CancellationToken cancellationToken = default)
        {
            status = value;
            return Task.CompletedTask;
        }
    }

    private sealed class FixedClock : IClock
    {
        public DateTimeOffset UtcNow { get; } = new(2026, 8, 21, 20, 0, 0, TimeSpan.Zero);
    }
}
