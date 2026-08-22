using MediaFlow.Web.Background;
using Microsoft.Extensions.Logging.Abstractions;

namespace MediaFlow.Tests;

public sealed class AutomationStatusTests
{
    [Fact]
    public void CompletedCycle_IsRestoredAfterRestart()
    {
        var directory = Path.Combine(Path.GetTempPath(), "mediaflow-status", Guid.NewGuid().ToString("N"));
        var path = Path.Combine(directory, "automation-status.json");
        try
        {
            var status = new AutomationStatus(path, NullLogger<AutomationStatus>.Instance);
            status.CycleStarted(new DateTimeOffset(2026, 8, 22, 10, 0, 0, TimeSpan.Zero));
            status.CycleCompleted(new DateTimeOffset(2026, 8, 22, 10, 1, 0, TimeSpan.Zero), 2, 15, 14, 0, 15, 0);

            var restored = new AutomationStatus(path, NullLogger<AutomationStatus>.Instance).Snapshot();

            Assert.False(restored.CycleRunning);
            Assert.Equal(15, restored.LastMatched);
            Assert.Equal(14, restored.LastWouldMove);
            Assert.Equal(2, restored.LastSourceShares);
        }
        finally
        {
            try { Directory.Delete(directory, recursive: true); } catch { }
        }
    }
}
