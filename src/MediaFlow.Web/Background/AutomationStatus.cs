namespace MediaFlow.Web.Background;

public sealed class AutomationStatus
{
    private readonly object _gate = new();
    private AutomationStatusSnapshot _snapshot = new(
        false,
        null,
        null,
        0,
        0,
        0,
        0,
        0,
        null);

    public AutomationStatusSnapshot Snapshot()
    {
        lock (_gate) return _snapshot;
    }

    public void CycleStarted(DateTimeOffset at)
    {
        lock (_gate)
        {
            _snapshot = _snapshot with
            {
                CycleRunning = true,
                LastCycleStartedAt = at,
                LastError = null
            };
        }
    }

    public void CycleCompleted(
        DateTimeOffset at,
        int sourceShares,
        int matched,
        int executed,
        int skipped,
        int errors)
    {
        lock (_gate)
        {
            _snapshot = new AutomationStatusSnapshot(
                false,
                _snapshot.LastCycleStartedAt,
                at,
                sourceShares,
                matched,
                executed,
                skipped,
                errors,
                null);
        }
    }

    public void CycleFailed(DateTimeOffset at, string error)
    {
        lock (_gate)
        {
            _snapshot = _snapshot with
            {
                CycleRunning = false,
                LastCycleCompletedAt = at,
                LastErrors = _snapshot.LastErrors + 1,
                LastError = error
            };
        }
    }
}

public sealed record AutomationStatusSnapshot(
    bool CycleRunning,
    DateTimeOffset? LastCycleStartedAt,
    DateTimeOffset? LastCycleCompletedAt,
    int LastSourceShares,
    int LastMatched,
    int LastExecuted,
    int LastSkipped,
    int LastErrors,
    string? LastError);
