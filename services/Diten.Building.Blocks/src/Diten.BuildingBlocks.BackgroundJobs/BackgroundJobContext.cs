namespace Diten.BuildingBlocks.BackgroundJobs;

public sealed record BackgroundJobContext(
    Guid? CorrelationId = null,
    Guid? CausationId = null,
    Guid? TenantId = null,
    string? EventName = null,
    Guid? EventId = null,
    string TriggerType = BackgroundJobTriggerTypes.FireAndForget,
    string? TriggeredBy = null,
    int RetryCount = 0,
    IReadOnlyDictionary<string, string>? Metadata = null)
{
    public Guid EffectiveCorrelationId => CorrelationId ?? Guid.NewGuid();
}
