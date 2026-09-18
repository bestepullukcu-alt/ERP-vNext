using Diten.ProcurementService.Domain.Entities;
using Diten.ProcurementService.Domain.Repositories;

namespace Diten.ProcurementService.Api.Tests.Sourcing;

/// <summary>
/// In-memory IRfxRepository test double. Modeller the PRODUCTION repository's binding contract: every read/write is
/// scoped to (TenantId, LegalEntityId, IsDeleted=false), exactly as RfxRepository.RfxTenantFilter()/BidTenantFilter()
/// enforce via Mongo. Two instances over the SAME shared stores but DIFFERENT legal entities let handler tests prove
/// cross-LE isolation (a record written under LE-A is invisible to an LE-B scope → handler 404). The Mongo filter
/// itself is pinned in RfxRepository.cs (no Mongo in unit env).
/// </summary>
public sealed class FakeRfxRepository : IRfxRepository
{
    private readonly List<RfxEvent> _rfx;
    private readonly List<Bid> _bids;
    private readonly Guid _tenantId;
    private readonly Guid _legalEntityId;

    public FakeRfxRepository(List<RfxEvent> sharedRfx, List<Bid> sharedBids, Guid tenantId, Guid legalEntityId)
    {
        _rfx = sharedRfx;
        _bids = sharedBids;
        _tenantId = tenantId;
        _legalEntityId = legalEntityId;
    }

    private IEnumerable<RfxEvent> ScopedRfx()
        => _rfx.Where(x => x.TenantId == _tenantId && x.LegalEntityId == _legalEntityId && !x.IsDeleted);

    private IEnumerable<Bid> ScopedBids()
        => _bids.Where(x => x.TenantId == _tenantId && x.LegalEntityId == _legalEntityId && !x.IsDeleted);

    // ── RFx events ───────────────────────────────────────────────────────────

    public Task<IReadOnlyList<RfxEvent>> GetAllAsync(RfxStatus? status = null, CancellationToken cancellationToken = default)
    {
        var items = ScopedRfx();
        if (status.HasValue)
        {
            items = items.Where(x => x.Status == status.Value);
        }
        return Task.FromResult<IReadOnlyList<RfxEvent>>(items.ToList());
    }

    public Task<RfxEvent?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => Task.FromResult(ScopedRfx().FirstOrDefault(x => x.Id == id));

    public Task<RfxEvent?> GetByRfxIdAsync(string rfxId, CancellationToken cancellationToken = default)
        => Task.FromResult(ScopedRfx().FirstOrDefault(x => x.RfxId == rfxId));

    public Task<RfxEvent?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken = default)
        => Task.FromResult(ScopedRfx().FirstOrDefault(x => x.IdempotencyKey == idempotencyKey));

    public Task<RfxEvent> CreateAsync(RfxEvent entity, CancellationToken cancellationToken = default)
    {
        entity.TenantId = _tenantId;
        entity.LegalEntityId = _legalEntityId;
        entity.IsDeleted = false;
        entity.CreatedAt = DateTimeOffset.UtcNow;
        entity.UpdatedAt = DateTimeOffset.UtcNow;
        _rfx.Add(entity);
        return Task.FromResult(entity);
    }

    public Task<bool> UpdateRfxAsync(RfxEvent entity, int expectedVersion, CancellationToken cancellationToken = default)
    {
        var existing = _rfx.FirstOrDefault(x =>
            x.TenantId == _tenantId && x.LegalEntityId == _legalEntityId && !x.IsDeleted
            && x.Id == entity.Id && x.Version == expectedVersion);
        if (existing is null)
        {
            return Task.FromResult(false);
        }

        entity.TenantId = _tenantId;
        entity.LegalEntityId = _legalEntityId;
        entity.UpdatedAt = DateTimeOffset.UtcNow;
        entity.Version = expectedVersion + 1;
        if (!ReferenceEquals(existing, entity))
        {
            _rfx.Remove(existing);
            _rfx.Add(entity);
        }
        return Task.FromResult(true);
    }

    public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var e = ScopedRfx().FirstOrDefault(x => x.Id == id);
        if (e is null)
        {
            return Task.FromResult(false);
        }
        e.IsDeleted = true;
        e.DeletedAt = DateTimeOffset.UtcNow;
        return Task.FromResult(true);
    }

    public Task<int> BulkDeleteAsync(IReadOnlyList<Guid> ids, CancellationToken cancellationToken = default)
    {
        var count = 0;
        foreach (var e in ScopedRfx().Where(x => ids.Contains(x.Id)).ToList())
        {
            e.IsDeleted = true;
            e.DeletedAt = DateTimeOffset.UtcNow;
            count++;
        }
        return Task.FromResult(count);
    }

    // ── Bids ────────────────────────────────────────────────────────────────

    public Task<Bid> CreateBidAsync(Bid entity, CancellationToken cancellationToken = default)
    {
        entity.TenantId = _tenantId;
        entity.LegalEntityId = _legalEntityId;
        entity.IsDeleted = false;
        entity.CreatedAt = DateTimeOffset.UtcNow;
        entity.UpdatedAt = DateTimeOffset.UtcNow;
        _bids.Add(entity);
        return Task.FromResult(entity);
    }

    public Task<IReadOnlyList<Bid>> GetBidsByRfxIdAsync(string rfxId, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<Bid>>(ScopedBids().Where(x => x.RfxId == rfxId).ToList());

    public Task<Bid?> GetBidByBidIdAsync(string bidId, CancellationToken cancellationToken = default)
        => Task.FromResult(ScopedBids().FirstOrDefault(x => x.BidId == bidId));

    public Task<Bid?> GetBidByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken = default)
        => Task.FromResult(ScopedBids().FirstOrDefault(x => x.IdempotencyKey == idempotencyKey));
}
