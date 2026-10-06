using Diten.ProcurementService.Domain.Entities;
using Diten.ProcurementService.Domain.Repositories;

namespace Diten.ProcurementService.Api.Tests.Contracting;

/// <summary>
/// In-memory IContractingRepository test double. Models the PRODUCTION repository's binding contract: every read/write
/// over both the contract and clause stores is scoped to (TenantId, LegalEntityId, IsDeleted=false), exactly as
/// ContractingRepository's tenant filters enforce via Mongo. Two instances over the SAME shared stores but DIFFERENT
/// legal entities let handler tests prove cross-LE isolation (a record written under LE-A is invisible to an LE-B scope
/// → handler 404). The Mongo filter itself is pinned in ContractingRepository.cs (no Mongo in unit env).
/// </summary>
public sealed class FakeContractingRepository : IContractingRepository
{
    private readonly List<Contract> _contracts;
    private readonly List<Clause> _clauses;
    private readonly Guid _tenantId;
    private readonly Guid _legalEntityId;

    public FakeContractingRepository(
        List<Contract> sharedContractStore,
        List<Clause> sharedClauseStore,
        Guid tenantId,
        Guid legalEntityId)
    {
        _contracts = sharedContractStore;
        _clauses = sharedClauseStore;
        _tenantId = tenantId;
        _legalEntityId = legalEntityId;
    }

    private IEnumerable<Contract> ScopedContracts()
        => _contracts.Where(x => x.TenantId == _tenantId && x.LegalEntityId == _legalEntityId && !x.IsDeleted);

    private IEnumerable<Clause> ScopedClauses()
        => _clauses.Where(x => x.TenantId == _tenantId && x.LegalEntityId == _legalEntityId && !x.IsDeleted);

    // ── Contract ─────────────────────────────────────────────────────────────

    public Task<IReadOnlyList<Contract>> GetAllAsync(string? supplierId = null, ContractStatus? status = null, CancellationToken cancellationToken = default)
    {
        var items = ScopedContracts();
        if (!string.IsNullOrWhiteSpace(supplierId))
        {
            items = items.Where(x => x.SupplierId == supplierId);
        }
        if (status.HasValue)
        {
            items = items.Where(x => x.Status == status.Value);
        }
        return Task.FromResult<IReadOnlyList<Contract>>(items.ToList());
    }

    public Task<Contract?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => Task.FromResult(ScopedContracts().FirstOrDefault(x => x.Id == id));

    public Task<Contract?> GetByContractIdAsync(string contractId, CancellationToken cancellationToken = default)
        => Task.FromResult(ScopedContracts().FirstOrDefault(x => x.ContractId == contractId));

    public Task<Contract?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken = default)
        => Task.FromResult(ScopedContracts().FirstOrDefault(x => x.IdempotencyKey == idempotencyKey));

    public Task<Contract> CreateAsync(Contract entity, CancellationToken cancellationToken = default)
    {
        entity.TenantId = _tenantId;
        entity.LegalEntityId = _legalEntityId;
        entity.IsDeleted = false;
        entity.CreatedAt = DateTimeOffset.UtcNow;
        entity.UpdatedAt = DateTimeOffset.UtcNow;
        _contracts.Add(entity);
        return Task.FromResult(entity);
    }

    public Task<bool> UpdateAsync(Contract entity, int expectedVersion, CancellationToken cancellationToken = default)
    {
        var existing = _contracts.FirstOrDefault(x =>
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
            _contracts.Remove(existing);
            _contracts.Add(entity);
        }
        return Task.FromResult(true);
    }

    public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var e = ScopedContracts().FirstOrDefault(x => x.Id == id);
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
        foreach (var e in ScopedContracts().Where(x => ids.Contains(x.Id)).ToList())
        {
            e.IsDeleted = true;
            e.DeletedAt = DateTimeOffset.UtcNow;
            count++;
        }
        return Task.FromResult(count);
    }

    // ── Clause library ────────────────────────────────────────────────────────

    public Task<Clause> CreateClauseAsync(Clause entity, CancellationToken cancellationToken = default)
    {
        entity.TenantId = _tenantId;
        entity.LegalEntityId = _legalEntityId;
        entity.IsDeleted = false;
        entity.CreatedAt = DateTimeOffset.UtcNow;
        entity.UpdatedAt = DateTimeOffset.UtcNow;
        _clauses.Add(entity);
        return Task.FromResult(entity);
    }

    public Task<Clause?> GetClauseByClauseIdAsync(string clauseId, CancellationToken cancellationToken = default)
        => Task.FromResult(ScopedClauses().FirstOrDefault(x => x.ClauseId == clauseId));

    public Task<Clause?> GetClauseByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken = default)
        => Task.FromResult(ScopedClauses().FirstOrDefault(x => x.IdempotencyKey == idempotencyKey));

    public Task<bool> ExistsClauseByCategoryTitleAsync(string category, string title, CancellationToken cancellationToken = default)
        => Task.FromResult(ScopedClauses().Any(x => x.Category == category && x.Title == title));

    public Task<bool> ExistsClauseAsync(string clauseId, CancellationToken cancellationToken = default)
        => Task.FromResult(ScopedClauses().Any(x => x.ClauseId == clauseId));

    public Task<IReadOnlyList<Clause>> ListClausesAsync(string? category = null, CancellationToken cancellationToken = default)
    {
        var items = ScopedClauses();
        if (!string.IsNullOrWhiteSpace(category))
        {
            items = items.Where(x => x.Category == category);
        }
        return Task.FromResult<IReadOnlyList<Clause>>(items.ToList());
    }
}
