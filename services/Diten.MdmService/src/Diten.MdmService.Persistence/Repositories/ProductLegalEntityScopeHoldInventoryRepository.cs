using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Repositories;
using MongoDB.Driver;

namespace Diten.MdmService.Persistence.Repositories;

/// <summary>
/// WP-MDM-SCOPE-ACCEPT-01 FIX2 (3) — the cross-tenant hold count behind the scope gauges. Like the recovery runners'
/// tenant discovery it reads across tenants, but it returns counts only (no identity leaves this class). It reads the
/// rollout records that hold anything — one small document per such tenant — through a projection of the hold fields.
/// </summary>
public sealed class ProductLegalEntityScopeHoldInventoryRepository : IProductLegalEntityScopeHoldInventoryRepository
{
    private readonly IMongoCollection<ProductLegalEntityScopeRolloutState> _states;

    public ProductLegalEntityScopeHoldInventoryRepository(IMongoDatabase database)
    {
        ArgumentNullException.ThrowIfNull(database);
        _states = database.GetCollection<ProductLegalEntityScopeRolloutState>(
            ProductLegalEntityScopeRolloutStateRepository.CollectionName);
    }

    public async Task<ProductLegalEntityScopeHoldInventory> ReadAsync(
        DateTimeOffset serverNowUtc,
        CancellationToken cancellationToken = default)
    {
        var filter = Builders<ProductLegalEntityScopeRolloutState>.Filter.Eq(item => item.IsDeleted, false)
                     & (Builders<ProductLegalEntityScopeRolloutState>.Filter.Ne(item => item.ActiveFence, null)
                        | Builders<ProductLegalEntityScopeRolloutState>.Filter.Ne(item => item.ActiveWriterLease, null)
                        | Builders<ProductLegalEntityScopeRolloutState>.Filter.SizeGt(item => item.ProductWriterLeases, 0));
        // Only the hold fields are read (the audit intents, the bulk of a record, stay on the server).
        var projection = Builders<ProductLegalEntityScopeRolloutState>.Projection
            .Include(item => item.ActiveFence)
            .Include(item => item.ActiveWriterLease)
            .Include(item => item.ProductWriterLeases);
        DateTimeOffset? oldest = null;
        long fences = 0, running = 0, expired = 0;
        using var cursor = await _states.Find(filter)
            .Project<ProductLegalEntityScopeRolloutState>(projection)
            .ToCursorAsync(cancellationToken);
        while (await cursor.MoveNextAsync(cancellationToken))
        {
            foreach (var holds in cursor.Current)
            {
                if (holds.ActiveFence is { } fence)
                {
                    fences++;
                    if (oldest is null || fence.AcquiredAtUtc < oldest) oldest = fence.AcquiredAtUtc;
                }
                var leases = (holds.ProductWriterLeases ?? []).AsEnumerable();
                if (holds.ActiveWriterLease is { } slot) leases = leases.Append(slot);
                foreach (var lease in leases)
                {
                    if (lease.IsExpiredAt(serverNowUtc)) expired++; else running++;
                }
            }
        }
        return new(oldest, fences, running, expired);
    }
}
