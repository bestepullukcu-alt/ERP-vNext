using Diten.ProcurementService.Domain.Entities;
using Diten.ProcurementService.Domain.Repositories;

namespace Diten.ProcurementService.Api.Tests.Suppliers;

/// <summary>
/// In-memory ISupplierRepository test double. Modeller the PRODUCTION repository's binding contract: every read/write
/// is scoped to (TenantId, LegalEntityId, IsDeleted=false), exactly as SupplierRepository.TenantFilter() enforces via
/// Mongo. Two instances over the SAME shared store but DIFFERENT legal entities let handler tests prove cross-LE
/// isolation (a record written under LE-A is invisible to an LE-B scope → handler 404). This is the seam the handlers
/// rely on; the Mongo filter itself is pinned in SupplierRepository.cs (no Mongo in unit env).
/// </summary>
public sealed class FakeSupplierRepository : ISupplierRepository
{
    private readonly List<Supplier> _store;
    private readonly Guid _tenantId;
    private readonly Guid _legalEntityId;

    public FakeSupplierRepository(List<Supplier> sharedStore, Guid tenantId, Guid legalEntityId)
    {
        _store = sharedStore;
        _tenantId = tenantId;
        _legalEntityId = legalEntityId;
    }

    private IEnumerable<Supplier> Scoped()
        => _store.Where(x => x.TenantId == _tenantId && x.LegalEntityId == _legalEntityId && !x.IsDeleted);

    public Task<IReadOnlyList<Supplier>> GetAllAsync(SupplierStatus? status = null, CancellationToken cancellationToken = default)
    {
        var items = Scoped();
        if (status.HasValue)
        {
            items = items.Where(x => x.Status == status.Value);
        }
        return Task.FromResult<IReadOnlyList<Supplier>>(items.ToList());
    }

    public Task<Supplier?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => Task.FromResult(Scoped().FirstOrDefault(x => x.Id == id));

    public Task<Supplier?> GetBySupplierIdAsync(string supplierId, CancellationToken cancellationToken = default)
        => Task.FromResult(Scoped().FirstOrDefault(x => x.SupplierId == supplierId));

    public Task<Supplier?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken = default)
        => Task.FromResult(Scoped().FirstOrDefault(x => x.IdempotencyKey == idempotencyKey));

    public Task<IReadOnlyList<Supplier>> GetBySupplierIdsAsync(IReadOnlyList<string> supplierIds, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<Supplier>>(Scoped().Where(x => supplierIds.Contains(x.SupplierId)).ToList());

    public Task<Supplier> CreateAsync(Supplier entity, CancellationToken cancellationToken = default)
    {
        entity.TenantId = _tenantId;
        entity.LegalEntityId = _legalEntityId;
        entity.IsDeleted = false;
        entity.CreatedAt = DateTimeOffset.UtcNow;
        entity.UpdatedAt = DateTimeOffset.UtcNow;
        _store.Add(entity);
        return Task.FromResult(entity);
    }

    public Task<bool> UpdateAsync(Supplier entity, int expectedVersion, CancellationToken cancellationToken = default)
    {
        // Optimistic concurrency + tenant/LE scope, mirroring the production Mongo filter.
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

    public Task<bool> ExistsByTaxIdAsync(string taxId, Guid? excludeId, CancellationToken cancellationToken = default)
        => Task.FromResult(Scoped().Any(x => x.TaxId == taxId && (!excludeId.HasValue || x.Id != excludeId.Value)));
}
