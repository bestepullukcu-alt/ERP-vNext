namespace Diten.InventoryService.Domain.Entities;

/// <summary>
/// Outbox row for the internal <c>InventoryReservationChanged</c> event, written to <c>inventory_outbox</c> in the same
/// transaction as the reservation change, Status <c>Pending</c> (no transport in S2 — integration-owned).
/// </summary>
public sealed class InventoryReservationOutboxMessage : EntityBase
{
    public const string ReservationChangedEventName = "InventoryReservationChanged";

    public string EventName { get; set; } = ReservationChangedEventName;

    public int EventVersion { get; set; } = 1;

    public Guid AggregateId { get; set; }

    public Guid CorrelationId { get; set; }

    public string Status { get; set; } = InventoryOutboxMessage.PendingStatus;

    public int AttemptCount { get; set; }

    public DateTimeOffset OccurredAt { get; set; }

    public InventoryReservationChangedPayload Payload { get; set; } = new();
}
