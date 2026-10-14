using Diten.MdmService.Application.Features.Approvals;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Repositories;
using MongoDB.Driver;

namespace Diten.MdmService.Persistence.Repositories;

/// <summary>
/// WP-VW-W4 (W4-BE-b2) — <c>mdm_competitor_products</c>, explicit tenant + compare-and-swap (the b1 store rule). The
/// company + name uniqueness is enforced in the service among the records that still count (a partial unique index would
/// need <c>$ne</c> / <c>$in</c> in its filter, which MongoDB rejects at start-up), so the index here only supports reads.
/// </summary>
public sealed class CompetitorProductRepository : ICompetitorProductRepository
{
    public const string CollectionName = "mdm_competitor_products";

    private readonly IMongoCollection<CompetitorProduct> _collection;

    public CompetitorProductRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<CompetitorProduct>(CollectionName);
        _collection.Indexes.CreateOne(new CreateIndexModel<CompetitorProduct>(
            Builders<CompetitorProduct>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.NormalizedKey),
            new CreateIndexOptions { Name = "ix_mdm_competitor_products_tenant_key" }));
    }

    public async Task<CompetitorProduct?> GetAsync(Guid tenantId, Guid id, CancellationToken ct)
        => tenantId == Guid.Empty
            ? null
            : await _collection.Find(x => x.TenantId == tenantId && x.Id == id && !x.IsDeleted).FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<CompetitorProduct>> ListAsync(Guid tenantId, CancellationToken ct)
        => tenantId == Guid.Empty ? [] : await _collection.Find(x => x.TenantId == tenantId && !x.IsDeleted).ToListAsync(ct);

    public async Task InsertAsync(CompetitorProduct record, CancellationToken ct)
    {
        if (record.TenantId == Guid.Empty) throw new InvalidOperationException("A competitor product needs a tenant.");
        record.CreatedAt = DateTimeOffset.UtcNow;
        record.UpdatedAt = record.CreatedAt;
        await _collection.InsertOneAsync(record, cancellationToken: ct);
    }

    public async Task<bool> ReplaceAsync(CompetitorProduct record, int expectedVersion, CancellationToken ct)
    {
        if (record.TenantId == Guid.Empty) return false;
        record.Version = expectedVersion + 1;
        record.UpdatedAt = DateTimeOffset.UtcNow;
        var result = await _collection.ReplaceOneAsync(
            x => x.TenantId == record.TenantId && x.Id == record.Id && x.Version == expectedVersion, record, cancellationToken: ct);
        if (result.MatchedCount == 1) return true;
        record.Version = expectedVersion;
        return false;
    }
}

/// <summary>WP-VW-W4 (W4-BE-b2) — <c>mdm_competitor_proposal_decisions</c>: the decision queries + the b1 approval store
/// (explicit tenant + CAS) the workflow applier uses.</summary>
public sealed class CompetitorProposalDecisionRepository : ICompetitorProposalDecisionRepository, IWorkflowApprovalStore<CompetitorProposalDecision>
{
    public const string CollectionName = "mdm_competitor_proposal_decisions";

    private readonly IMongoCollection<CompetitorProposalDecision> _collection;
    private readonly MongoWorkflowApprovalStore<CompetitorProposalDecision> _store;

    public CompetitorProposalDecisionRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<CompetitorProposalDecision>(CollectionName);
        _store = new MongoWorkflowApprovalStore<CompetitorProposalDecision>(database, CollectionName);
        _collection.Indexes.CreateOne(new CreateIndexModel<CompetitorProposalDecision>(
            Builders<CompetitorProposalDecision>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.ProposalId),
            new CreateIndexOptions { Name = "ix_mdm_competitor_decisions_tenant_proposal" }));
    }

    public Task<CompetitorProposalDecision?> GetAsync(Guid tenantId, Guid id, CancellationToken ct) => _store.GetAsync(tenantId, id, ct);

    public async Task<CompetitorProposalDecision?> GetByProposalAsync(Guid tenantId, Guid proposalId, CancellationToken ct)
        => tenantId == Guid.Empty
            ? null
            : (await _collection.Find(x => x.TenantId == tenantId && x.ProposalId == proposalId && !x.IsDeleted).ToListAsync(ct))
                .OrderByDescending(x => x.CreatedAt).FirstOrDefault();

    public async Task<IReadOnlyList<CompetitorProposalDecision>> ListByProposalsAsync(Guid tenantId, IReadOnlyCollection<Guid> proposalIds,
        CancellationToken ct)
        => tenantId == Guid.Empty || proposalIds.Count == 0
            ? []
            : await _collection.Find(Builders<CompetitorProposalDecision>.Filter.And(
                Builders<CompetitorProposalDecision>.Filter.Eq(x => x.TenantId, tenantId),
                Builders<CompetitorProposalDecision>.Filter.In(x => x.ProposalId, proposalIds),
                Builders<CompetitorProposalDecision>.Filter.Eq(x => x.IsDeleted, false))).ToListAsync(ct);

    public async Task InsertAsync(CompetitorProposalDecision record, CancellationToken ct)
    {
        if (record.TenantId == Guid.Empty) throw new InvalidOperationException("A decision needs a tenant.");
        record.CreatedAt = DateTimeOffset.UtcNow;
        await _collection.InsertOneAsync(record, cancellationToken: ct);
    }

    public Task<bool> ReplaceAsync(CompetitorProposalDecision record, int expectedVersion, CancellationToken ct)
        => _store.ReplaceAsync(record, expectedVersion, ct);
}
