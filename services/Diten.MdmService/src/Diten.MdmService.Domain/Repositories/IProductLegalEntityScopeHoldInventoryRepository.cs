namespace Diten.MdmService.Domain.Repositories;

/// <summary>
/// WP-MDM-SCOPE-ACCEPT-01 FIX2 (3) — what holds product writes NOW, across every tenant, as counts only: the fences up
/// (and the oldest one's start, since a fence has no expiry) and the writer leases held, running or expired. Read-only;
/// it feeds the operational gauges and names no tenant, product or person.
/// </summary>
public interface IProductLegalEntityScopeHoldInventoryRepository
{
    Task<ProductLegalEntityScopeHoldInventory> ReadAsync(
        DateTimeOffset serverNowUtc,
        CancellationToken cancellationToken = default);
}

public sealed record ProductLegalEntityScopeHoldInventory(
    DateTimeOffset? OldestFenceAcquiredAtUtc,
    long FencesUp,
    long RunningLeases,
    long ExpiredLeases);
