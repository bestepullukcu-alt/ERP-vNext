namespace Diten.InventoryService.Domain.Entities;

/// <summary>
/// Outbox row for the internal <c>InventoryMovementPosted</c> event (pack §3, ASSUMPTION-0173-08). Written in the
/// posting transaction with <see cref="Status"/> = <c>Pending</c>; no transport is wired in S1 (integration-owned),
/// so rows stay Pending. The status vocabulary mirrors <c>Diten.BuildingBlocks.Eventing.EventOutboxDeliveryStatus</c>.
/// </summary>
public sealed class InventoryOutboxMessage : EntityBase
{
    public const string PendingStatus = "Pending";

    public const string MovementPostedEventName = "InventoryMovementPosted";

    public string EventName { get; set; } = MovementPostedEventName;

    public int EventVersion { get; set; } = 1;

    public Guid AggregateId { get; set; }

    public Guid CorrelationId { get; set; }

    public string Status { get; set; } = PendingStatus;

    public int AttemptCount { get; set; }

    public DateTimeOffset OccurredAt { get; set; }

    public InventoryMovementPostedPayload Payload { get; set; } = new();
}
