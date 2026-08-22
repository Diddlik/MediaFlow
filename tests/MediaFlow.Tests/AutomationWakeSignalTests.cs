using MediaFlow.Web.Background;

namespace MediaFlow.Tests;

public sealed class AutomationWakeSignalTests
{
    [Fact]
    public async Task Wake_ReleasesPendingWait()
    {
        var signal = new AutomationWakeSignal();
        signal.Wake();

        await signal.WaitAsync(TimeSpan.FromHours(1), CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(1));
    }
}
