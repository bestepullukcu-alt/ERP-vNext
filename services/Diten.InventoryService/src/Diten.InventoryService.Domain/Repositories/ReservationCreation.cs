using Diten.InventoryService.Domain.Entities;

namespace Diten.InventoryService.Domain.Repositories;

/// <summary>A reservation to create, with the audit record and outbox event written atomically with it.</summary>
public sealed record ReservationCreation(
    InventoryReservation Reservation,
    InventoryReservationAuditRecord Audit,
    InventoryReservationOutboxMessage Outbox);
