using Diten.ProcurementService.Domain.Entities;
using Diten.ProcurementService.Domain.Repositories;

namespace Diten.ProcurementService.Api.Tests.Grn;

/// <summary>
/// In-memory IGrnRepository test double. Models the PRODUCTION repository's binding contract: every read/write is
/// scoped to (TenantId, LegalEntityId, IsDeleted=false), exactly as GrnRepository.TenantFilter() enforces via Mongo.
/// Two instances over the SAME shared store but DIFFERENT legal entities let handler tests prove cross-LE isolation
/// (a record written under LE-A is invisible to an LE-B scope → handler 404). The Mongo filter itself is pinned in
/// GrnRepository.cs (no Mongo in unit env).
/// </summary>
public sealed class FakeGrnRepository : IGrnRepository
{
    private readonly List<GoodsReceipt> _store;
    private readonly Guid _tenantId;
    private readonly Guid _legalEntityId;

    public FakeGrnRepository(List<GoodsReceipt> sharedStore, Guid tenantId, Guid legalEntityId)
    {
        _store = sharedStore;
        _tenantId = tenantId;
        _legalEntityId = legalEntityId;
    }

    private IEnumerable<GoodsReceipt> Scoped()
        => _store.Where(x => x.TenantId == _tenantId && x.LegalEntityId == _legalEntityId && !x.IsDeleted);

    public Task<IReadOnlyList<GoodsReceipt>> GetAllAsync(string? poId = null, GrnStatus? status = null, CancellationToken cancellationToken = default)
    {
        var items = Scoped();
        if (!string.IsNullOrWhiteSpace(poId))
        {
            items = items.Where(x => x.PoId == poId);
        }
        if (status.HasValue)
        {
            items = items.Where(x => x.Status == status.Value);
        }
        return Task.FromResult<IReadOnlyList<GoodsReceipt>>(items.ToList());
    }

    public Task<GoodsReceipt?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => Task.FromResult(Scoped().FirstOrDefault(x => x.Id == id));

    public Task<GoodsReceipt?> GetByGrnIdAsync(string grnId, CancellationToken cancellationToken = default)
        => Task.FromResult(Scoped().FirstOrDefault(x => x.GrnId == grnId));

    public Task<GoodsReceipt?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken = default)
        => Task.FromResult(Scoped().FirstOrDefault(x => x.IdempotencyKey == idempotencyKey));

    public Task<GoodsReceipt> CreateAsync(GoodsReceipt entity, CancellationToken cancellationToken = default)
    {
        entity.TenantId = _tenantId;
        entity.LegalEntityId = _legalEntityId;
        entity.IsDeleted = false;
        entity.CreatedAt = DateTimeOffset.UtcNow;
        entity.UpdatedAt = DateTimeOffset.UtcNow;
        _store.Add(entity);
        return Task.FromResult(entity);
    }

    public Task<bool> UpdateAsync(GoodsReceipt entity, int expectedVersion, CancellationToken cancellationToken = default)
    {
        var existing = _store.FirstOrDefault(x =>
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
            _store.Remove(existing);
            _store.Add(entity);
        }
        return Task.FromResult(true);
    }

    public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var e = Scoped().FirstOrDefault(x => x.Id == id);
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
        foreach (var e in Scoped().Where(x => ids.Contains(x.Id)).ToList())
        {
            e.IsDeleted = true;
            e.DeletedAt = DateTimeOffset.UtcNow;
            count++;
        }
        return Task.FromResult(count);
    }
}
