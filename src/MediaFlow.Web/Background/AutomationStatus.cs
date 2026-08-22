using System.Text.Json;

namespace MediaFlow.Web.Background;

public sealed class AutomationStatus
{
    private readonly object _gate = new();
    private readonly string _path;
    private readonly ILogger<AutomationStatus> _logger;
    private AutomationStatusSnapshot _snapshot;

    public AutomationStatus(string path, ILogger<AutomationStatus> logger)
    {
        _path = Path.GetFullPath(path);
        _logger = logger;
        _snapshot = Load();
    }

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
                CurrentShareName = null,
                CurrentPhase = "Preparing",
                CurrentProcessed = 0,
                CurrentTotal = 0,
                CurrentMatched = 0,
                CurrentWouldMove = 0,
                LastError = null
            };
        }
    }

    public void Progress(string shareName, string phase, int processed, int total)
    {
        lock (_gate)
        {
            _snapshot = _snapshot with
            {
                CurrentShareName = shareName,
                CurrentPhase = phase,
                CurrentProcessed = processed,
                CurrentTotal = total
            };
        }
    }

    public void MatchFound()
    {
        lock (_gate) _snapshot = _snapshot with { CurrentMatched = _snapshot.CurrentMatched + 1 };
    }

    public void WouldMoveFound()
    {
        lock (_gate) _snapshot = _snapshot with { CurrentWouldMove = _snapshot.CurrentWouldMove + 1 };
    }

    public void CycleCompleted(
        DateTimeOffset at,
        int sourceShares,
        int matched,
        int wouldMove,
        int executed,
        int skipped,
        int errors)
    {
        lock (_gate)
        {
            _snapshot = _snapshot with
            {
                CycleRunning = false,
                LastCycleCompletedAt = at,
                LastSourceShares = sourceShares,
                LastMatched = matched,
                LastWouldMove = wouldMove,
                LastExecuted = executed,
                LastSkipped = skipped,
                LastErrors = errors,
                CurrentPhase = "Completed",
                CurrentProcessed = _snapshot.CurrentTotal,
                LastError = null
            };
            Persist();
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
                CurrentPhase = "Failed",
                LastError = error
            };
            Persist();
        }
    }

    private AutomationStatusSnapshot Load()
    {
        try
        {
            if (!File.Exists(_path)) return new();
            var saved = JsonSerializer.Deserialize<AutomationStatusSnapshot>(File.ReadAllText(_path));
            return (saved ?? new()) with
            {
                CycleRunning = false,
                CurrentShareName = null,
                CurrentPhase = null,
                CurrentProcessed = 0,
                CurrentTotal = 0,
                CurrentMatched = 0,
                CurrentWouldMove = 0
            };
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            _logger.LogWarning(ex, "Could not load persisted automation status from {Path}", _path);
            return new();
        }
    }

    private void Persist()
    {
        try
        {
            var directory = Path.GetDirectoryName(_path);
            if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
            var tempPath = _path + ".tmp";
            File.WriteAllText(tempPath, JsonSerializer.Serialize(_snapshot));
            File.Move(tempPath, _path, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "Could not persist automation status to {Path}", _path);
        }
    }
}

public sealed record AutomationStatusSnapshot
{
    public bool CycleRunning { get; init; }
    public DateTimeOffset? LastCycleStartedAt { get; init; }
    public DateTimeOffset? LastCycleCompletedAt { get; init; }
    public int LastSourceShares { get; init; }
    public int LastMatched { get; init; }
    public int LastWouldMove { get; init; }
    public int LastExecuted { get; init; }
    public int LastSkipped { get; init; }
    public int LastErrors { get; init; }
    public string? LastError { get; init; }
    public string? CurrentShareName { get; init; }
    public string? CurrentPhase { get; init; }
    public int CurrentProcessed { get; init; }
    public int CurrentTotal { get; init; }
    public int CurrentMatched { get; init; }
    public int CurrentWouldMove { get; init; }
}
