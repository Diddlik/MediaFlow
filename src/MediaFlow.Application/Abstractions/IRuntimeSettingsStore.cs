namespace MediaFlow.Application.Abstractions;

public sealed record MediaFlowRuntimeSettings(
    bool DryRun = true,
    bool AutomationEnabled = true,
    int ReconciliationIntervalSeconds = 300,
    int MaxFilesPerSharePerCycle = 200,
    bool AllowFilesystemTimestampFallback = false);

public interface IRuntimeSettingsStore
{
    Task<MediaFlowRuntimeSettings> GetAsync(CancellationToken cancellationToken = default);
    Task<MediaFlowRuntimeSettings> UpdateAsync(
        MediaFlowRuntimeSettings settings,
        CancellationToken cancellationToken = default);
    Task<MediaFlowRuntimeSettings> ResetAsync(CancellationToken cancellationToken = default);
}
