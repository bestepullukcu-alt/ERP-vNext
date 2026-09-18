using Diten.ProcurementService.Application.Common;
using Diten.ProcurementService.Domain.Entities;
using Diten.ProcurementService.Domain.Repositories;
using MongoDB.Driver;

namespace Diten.ProcurementService.Persistence.Repositories;

/// <summary>
/// Contract + Clause-library Mongo repository (MOD-0144). Tek yazıcı (K15). HER sorgu Tenant + LegalEntity +
/// IsDeleted=false ile filtrelenir (multi-tenancy.md hard rules; cross-tenant/LE → boş sonuç → handler 404).
/// Soft-delete zorunlu; hard delete YOK. State geçişi (activate) optimistic concurrency: Version filtresi + increment.
/// Clause IMMUTABLE (ASSUMPTION-0144-03) — clause update/delete metodu YOK. Doküman binary saklanmaz (yalnız
/// Contract.EvidenceRefs referansları).
/// </summary>
public sealed class ContractingRepository : IContractingRepository
{
    private readonly IMongoCollection<Contract> _contracts;
    private readonly IMongoCollection<Clause> _clauses;
    private readonly Guid _tenantId;
    private readonly Guid _legalEntityId;

    public ContractingRepository(IMongoDatabase database, ITenantContext tenantContext)
    {
        _contracts = database.GetCollection<Contract>(ContractingIndexConfiguration.ContractCollectionName);
        _clauses = database.GetCollection<Clause>(ContractingIndexConfiguration.ClauseCollectionName);
        _tenantId = tenantContext.TenantId;
        _legalEntityId = tenantContext.LegalEntityId;
    }

    // ── Contract ─────────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<Contract>> GetAllAsync(string? supplierId = null, ContractStatus? status = null, CancellationToken cancellationToken = default)
    {
        var filter = ContractTenantFilter();
        if (!string.IsNullOrWhiteSpace(supplierId))
        {
            filter &= Builders<Contract>.Filter.Eq(x => x.SupplierId, supplierId);
        }
        if (status.HasValue)
        {
            filter &= Builders<Contract>.Filter.Eq(x => x.Status, status.Value);
        }
        return await _contracts.Find(filter).SortBy(x => x.ContractId).ToListAsync(cancellationToken);
    }

    public async Task<Contract?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var filter = Builders<Contract>.Filter.And(
            ContractTenantFilter(),
            Builders<Contract>.Filter.Eq(x => x.Id, id));
        return await _contracts.Find(filter).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<Contract?> GetByContractIdAsync(string contractId, CancellationToken cancellationToken = default)
    {
        var filter = Builders<Contract>.Filter.And(
            ContractTenantFilter(),
            Builders<Contract>.Filter.Eq(x => x.ContractId, contractId));
        return await _contracts.Find(filter).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<Contract?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken = default)
    {
        var filter = Builders<Contract>.Filter.And(
            ContractTenantFilter(),
            Builders<Contract>.Filter.Eq(x => x.IdempotencyKey, idempotencyKey));
        return await _contracts.Find(filter).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<Contract> CreateAsync(Contract entity, CancellationToken cancellationToken = default)
    {
        entity.TenantId = _tenantId;
        entity.LegalEntityId = _legalEntityId;
        entity.CreatedAt = DateTimeOffset.UtcNow;
        entity.UpdatedAt = DateTimeOffset.UtcNow;
        entity.IsDeleted = false;
        await _contracts.InsertOneAsync(entity, cancellationToken: cancellationToken);
        return entity;
    }

    public async Task<bool> UpdateAsync(Contract entity, int expectedVersion, CancellationToken cancellationToken = default)
    {
        entity.TenantId = _tenantId;
        entity.LegalEntityId = _legalEntityId;
        entity.UpdatedAt = DateTimeOffset.UtcNow;

        var filter = Builders<Contract>.Filter.And(
            ContractTenantFilter(),
            Builders<Contract>.Filter.Eq(x => x.Id, entity.Id),
            Builders<Contract>.Filter.Eq(x => x.Version, expectedVersion));

        entity.Version = expectedVersion + 1;
        var result = await _contracts.ReplaceOneAsync(filter, entity, cancellationToken: cancellationToken);
        return result.ModifiedCount > 0;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var filter = Builders<Contract>.Filter.And(
            ContractTenantFilter(),
            Builders<Contract>.Filter.Eq(x => x.Id, id));

        var update = Builders<Contract>.Update
            .Set(x => x.IsDeleted, true)
            .Set(x => x.DeletedAt, DateTimeOffset.UtcNow)
            .Set(x => x.UpdatedAt, DateTimeOffset.UtcNow);

        var result = await _contracts.UpdateOneAsync(filter, update, cancellationToken: cancellationToken);
        return result.ModifiedCount > 0;
    }

    public async Task<int> BulkDeleteAsync(IReadOnlyList<Guid> ids, CancellationToken cancellationToken = default)
    {
        var filter = Builders<Contract>.Filter.And(
            ContractTenantFilter(),
            Builders<Contract>.Filter.In(x => x.Id, ids));

        var update = Builders<Contract>.Update
            .Set(x => x.IsDeleted, true)
            .Set(x => x.DeletedAt, DateTimeOffset.UtcNow)
            .Set(x => x.UpdatedAt, DateTimeOffset.UtcNow);

        var result = await _contracts.UpdateManyAsync(filter, update, cancellationToken: cancellationToken);
        return (int)result.ModifiedCount;
    }

    // ── Clause library ────────────────────────────────────────────────────────

    public async Task<Clause> CreateClauseAsync(Clause entity, CancellationToken cancellationToken = default)
    {
        entity.TenantId = _tenantId;
        entity.LegalEntityId = _legalEntityId;
        entity.CreatedAt = DateTimeOffset.UtcNow;
        entity.UpdatedAt = DateTimeOffset.UtcNow;
        entity.IsDeleted = false;
        await _clauses.InsertOneAsync(entity, cancellationToken: cancellationToken);
        return entity;
    }

    public async Task<Clause?> GetClauseByClauseIdAsync(string clauseId, CancellationToken cancellationToken = default)
    {
        var filter = Builders<Clause>.Filter.And(
            ClauseTenantFilter(),
            Builders<Clause>.Filter.Eq(x => x.ClauseId, clauseId));
        return await _clauses.Find(filter).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<Clause?> GetClauseByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken = default)
    {
        var filter = Builders<Clause>.Filter.And(
            ClauseTenantFilter(),
            Builders<Clause>.Filter.Eq(x => x.IdempotencyKey, idempotencyKey));
        return await _clauses.Find(filter).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<bool> ExistsClauseByCategoryTitleAsync(string category, string title, CancellationToken cancellationToken = default)
    {
        var filter = Builders<Clause>.Filter.And(
            ClauseTenantFilter(),
            Builders<Clause>.Filter.Eq(x => x.Category, category),
            Builders<Clause>.Filter.Eq(x => x.Title, title));
        return await _clauses.Find(filter).AnyAsync(cancellationToken);
    }

    public async Task<bool> ExistsClauseAsync(string clauseId, CancellationToken cancellationToken = default)
    {
        var filter = Builders<Clause>.Filter.And(
            ClauseTenantFilter(),
            Builders<Clause>.Filter.Eq(x => x.ClauseId, clauseId));
        return await _clauses.Find(filter).AnyAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Clause>> ListClausesAsync(string? category = null, CancellationToken cancellationToken = default)
    {
        var filter = ClauseTenantFilter();
        if (!string.IsNullOrWhiteSpace(category))
        {
            filter &= Builders<Clause>.Filter.Eq(x => x.Category, category);
        }
        return await _clauses.Find(filter).SortBy(x => x.ClauseId).ToListAsync(cancellationToken);
    }

    /// <summary>Tenant + LegalEntity + IsDeleted=false — her Contract sorgusunun zorunlu giriş filtresi.</summary>
    private FilterDefinition<Contract> ContractTenantFilter()
        => Builders<Contract>.Filter.And(
            Builders<Contract>.Filter.Eq(x => x.TenantId, (Guid?)_tenantId),
            Builders<Contract>.Filter.Eq(x => x.LegalEntityId, (Guid?)_legalEntityId),
            Builders<Contract>.Filter.Eq(x => x.IsDeleted, false));

    /// <summary>Tenant + LegalEntity + IsDeleted=false — her Clause sorgusunun zorunlu giriş filtresi.</summary>
    private FilterDefinition<Clause> ClauseTenantFilter()
        => Builders<Clause>.Filter.And(
            Builders<Clause>.Filter.Eq(x => x.TenantId, (Guid?)_tenantId),
            Builders<Clause>.Filter.Eq(x => x.LegalEntityId, (Guid?)_legalEntityId),
            Builders<Clause>.Filter.Eq(x => x.IsDeleted, false));
}
