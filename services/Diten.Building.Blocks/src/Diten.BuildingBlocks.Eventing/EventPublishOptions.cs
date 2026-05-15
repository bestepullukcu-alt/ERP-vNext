namespace Diten.BuildingBlocks.Eventing;

public sealed record EventPublishOptions
{
    public Guid? EventId { get; init; }

    public Guid? CorrelationId { get; init; }

    public Guid? CausationId { get; init; }

    public Guid? TenantId { get; init; }

    public string? Producer { get; init; }

    public DateTimeOffset? OccurredAtUtc { get; init; }
}
