namespace Diten.Platform.Domain.Enums;

public enum PersonReferenceCorrelationState
{
    Deferred = 1,
    Validated = 2,
    Conflict = 3,
    Suspended = 4,
    Archived = 5
}
