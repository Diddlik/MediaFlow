namespace MediaFlow.Core.Domain;

public enum ShareRole
{
    Source,
    Destination,
    Both
}

public enum MediaType
{
    Image,
    Video,
    Other
}

public enum MediaEventStatus
{
    Planned,
    Active,
    Closed,
    Archived,
    Cancelled
}

public enum OperationMode
{
    Copy,
    SafeMove,
    Archive
}

public enum ConflictStrategy
{
    AppendSourceName,
    AppendCounter,
    Quarantine
}

public enum DuplicateStrategy
{
    KeepExisting,
    KeepBoth,
    SkipAndRecord,
    SafeMoveToExisting
}
