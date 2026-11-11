using System.Collections.Concurrent;
using Diten.InventoryService.Application.Common;

namespace Diten.InventoryService.Tests.TestInfrastructure;

/// <summary>
/// WP-MVP1-NAMES1 — the PRODUCTION name reader (<see cref="Persistence.CentralAuditActorNames"/>) behind a pass-through that records each read
/// (tenant, number of asked pairs, company bound) — the measure of "one batch read per response, never one per row". It changes nothing it reads.
/// </summary>
public sealed class CountingAuditActorNames(IAuditActorNames inner, ITenantContext tenant, ConcurrentQueue<AuditNameRead> reads) : IAuditActorNames
{
    public Task<IReadOnlyDictionary<(Guid CorrelationId, Guid ActorId), string>> NamesAsync(
        IReadOnlyCollection<(Guid CorrelationId, Guid ActorId)> keys, CancellationToken cancellationToken = default)
    {
        reads.Enqueue(new AuditNameRead(tenant.IsResolved ? tenant.TenantId : Guid.Empty, keys.Count, null));
        return inner.NamesAsync(keys, cancellationToken);
    }

    public Task<IReadOnlyDictionary<(Guid CorrelationId, Guid ActorId), string>> NamesAsync(
        IReadOnlyCollection<(Guid CorrelationId, Guid ActorId)> keys, Guid companyId, CancellationToken cancellationToken = default)
    {
        reads.Enqueue(new AuditNameRead(tenant.IsResolved ? tenant.TenantId : Guid.Empty, keys.Count, companyId));
        return inner.NamesAsync(keys, companyId, cancellationToken);
    }
}

/// <summary>One recorded name read: the caller's tenant, how many (correlation, user) pairs it asked, the company it was bound to (null = tenant-wide).</summary>
public sealed record AuditNameRead(Guid Tenant, int Keys, Guid? Company);
