namespace Diten.InventoryService.Domain.Repositories;

/// <summary>
/// System-level expiry of due reservations across all tenants (pack ASSUMPTION-0173-16 "periodic hosted sweeper").
/// Each SKU is processed in its own transaction with an explicit Tenant + LE scope — never a cross-tenant write.
/// </summary>
public interface IReservationExpirySweeper
{
    /// <summary>Expires every ACTIVE reservation with <c>expiresAt ≤ now</c>; returns how many were expired.</summary>
    Task<int> SweepAsync(DateTimeOffset now, CancellationToken cancellationToken = default);
}
