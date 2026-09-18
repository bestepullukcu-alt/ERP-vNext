using Diten.ProcurementService.Domain.Entities;
using Diten.ProcurementService.Domain.Repositories;

namespace Diten.ProcurementService.Api.Tests.RequisitionPo;

/// <summary>
/// In-memory IRequisitionRepository test double. Modeller the PRODUCTION repository's binding contract: every
/// read/write is scoped to (TenantId, LegalEntityId, IsDeleted=false), exactly as RequisitionRepository.TenantFilter()
/// enforces via Mongo. Two instances over the SAME shared store but DIFFERENT legal entities let handler tests prove
/// cross-LE isolation (a record written under LE-A is invisible to an LE-B scope → handler 404). The Mongo filter
/// itself is pinned in RequisitionRepository.cs (no Mongo in unit env).
/// </summary>
public sealed class FakeRequisitionRepository : IRequisitionRepository
{
    private readonly List<Requisition> _store;
    private readonly Guid _tenantId;
    private readonly Guid _legalEntityId;

    public FakeRequisitionRepository(List<Requisition> sharedStore, Guid tenantId, Guid legalEntityId)
    {
        _store = sharedStore;
        _tenantId = tenantId;
        _legalEntityId = legalEntityId;
    }

    private IEnumerable<Requisition> Scoped()
        => _store.Where(x => x.TenantId == _tenantId && x.LegalEntityId == _legalEntityId && !x.IsDeleted);

    public Task<IReadOnlyList<Requisition>> GetAllAsync(RequisitionStatus? status = null, CancellationToken cancellationToken = default)
    {
        var items = Scoped();
        if (status.HasValue)
        {
            items = items.Where(x => x.Status == status.Value);
        }
        return Task.FromResult<IReadOnlyList<Requisition>>(items.ToList());
    }

    public Task<Requisition?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => Task.FromResult(Scoped().FirstOrDefault(x => x.Id == id));

    public Task<Requisition?> GetByRequisitionIdAsync(string requisitionId, CancellationToken cancellationToken = default)
        => Task.FromResult(Scoped().FirstOrDefault(x => x.RequisitionId == requisitionId));

    public Task<Requisition?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken = default)
        => Task.FromResult(Scoped().FirstOrDefault(x => x.IdempotencyKey == idempotencyKey));

    public Task<Requisition> CreateAsync(Requisition entity, CancellationToken cancellationToken = default)
    {
        entity.TenantId = _tenantId;
        entity.LegalEntityId = _legalEntityId;
        entity.IsDeleted = false;
        entity.CreatedAt = DateTimeOffset.UtcNow;
        entity.UpdatedAt = DateTimeOffset.UtcNow;
        _store.Add(entity);
        return Task.FromResult(entity);
    }

    public Task<bool> UpdateAsync(Requisition entity, int expectedVersion, CancellationToken cancellationToken = default)
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
