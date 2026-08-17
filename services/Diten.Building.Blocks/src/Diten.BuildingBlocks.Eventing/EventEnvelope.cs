namespace Diten.BuildingBlocks.Eventing;

public sealed record EventEnvelope<TPayload>(EventMetadata Metadata, TPayload Payload)
    where TPayload : IIntegrationEvent
{
    public Guid EventId => Metadata.EventId;

    public string EventName => Metadata.EventName;

    public int EventVersion => Metadata.EventVersion;

    public Guid CorrelationId => Metadata.CorrelationId;

    public Guid? CausationId => Metadata.CausationId;

    public Guid? TenantId => Metadata.TenantId;

    public string Producer => Metadata.Producer;

    public DateTimeOffset OccurredAtUtc => Metadata.OccurredAtUtc;
}
