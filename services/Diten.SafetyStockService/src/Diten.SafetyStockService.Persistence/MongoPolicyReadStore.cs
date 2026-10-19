using Diten.SafetyStockService.Application;
using Diten.SafetyStockService.Domain;
using MongoDB.Driver;

namespace Diten.SafetyStockService.Persistence;

public sealed class MongoPolicyReadStore : IPolicyReadStore
{
    private readonly SafetyStockMongoContext _context;

    public MongoPolicyReadStore(SafetyStockMongoContext context) => _context = context;

    public async Task<IReadOnlyList<PolicyReadModel>> ListAsync(Guid tenantId, Guid legalEntityId,
        ActorSeriesScope scope, CancellationToken cancellationToken)
    {
        var policies = await _context.Policies.Find(PolicyFilter(tenantId, legalEntityId)).ToListAsync(cancellationToken);
        var results = new List<PolicyReadModel>();
        foreach (var policy in policies)
        {
            var series = Series(policy);
            if (!scope.Contains(series)) continue;
            var revision = await CurrentRevisionAsync(policy, cancellationToken);
            results.Add(new(Guid.Parse(policy.Id), tenantId, legalEntityId, series, revision.State, policy.Version));
        }
        return results;
    }

    public async Task<PolicyReadModel?> GetPolicyAsync(Guid tenantId, Guid legalEntityId, Guid policyId,
        ActorSeriesScope scope, CancellationToken cancellationToken)
    {
        var policy = await _context.Policies.Find(PolicyFilter(tenantId, legalEntityId)
            & Builders<PolicyDocument>.Filter.Eq(p => p.Id, policyId.ToString("D"))).FirstOrDefaultAsync(cancellationToken);
        if (policy is null || !scope.Contains(Series(policy))) return null;
        var revision = await CurrentRevisionAsync(policy, cancellationToken);
        return new(policyId, tenantId, legalEntityId, Series(policy), revision.State, policy.Version);
    }

    public async Task<PolicyRevisionReadModel?> GetRevisionAsync(Guid tenantId, Guid legalEntityId, Guid policyId,
        Guid revisionId, ActorSeriesScope scope, CancellationToken cancellationToken)
    {
        var policy = await _context.Policies.Find(PolicyFilter(tenantId, legalEntityId)
            & Builders<PolicyDocument>.Filter.Eq(p => p.Id, policyId.ToString("D"))).FirstOrDefaultAsync(cancellationToken);
        if (policy is null || !scope.Contains(Series(policy))) return null;
        var revision = await _context.Revisions.Find(Builders<PolicyRevisionDocument>.Filter.Where(r =>
            r.Id == revisionId.ToString("D") && r.PolicyId == policy.Id && r.TenantId == policy.TenantId
            && r.LegalEntityId == policy.LegalEntityId && !r.IsDeleted)).FirstOrDefaultAsync(cancellationToken);
        if (revision is null) return null;
        if (revision.SkuId != policy.SkuId || revision.WarehouseId != policy.WarehouseId)
            throw new InvalidOperationException("Policy revision scope mismatch.");
        await RequireAuditAsync(revision, cancellationToken);
        return new(revisionId, policyId, tenantId, legalEntityId, Series(policy), revision.State, revision.Version);
    }

    public Task<PolicyEvaluationReadModel?> GetEvaluationAsync(Guid tenantId, Guid legalEntityId, Guid policyId,
        Guid evaluationId, ActorSeriesScope scope, CancellationToken cancellationToken)
        => throw new InvalidOperationException("Safety stock evaluation persistence is not configured.");

    private async Task<PolicyRevisionDocument> CurrentRevisionAsync(PolicyDocument policy, CancellationToken ct)
    {
        var revision = await _context.Revisions.Find(Builders<PolicyRevisionDocument>.Filter.Where(r =>
            r.Id == policy.CurrentRevisionId && r.PolicyId == policy.Id && r.TenantId == policy.TenantId
            && r.LegalEntityId == policy.LegalEntityId && !r.IsDeleted)).FirstOrDefaultAsync(ct);
        if (revision is null || revision.SkuId != policy.SkuId || revision.WarehouseId != policy.WarehouseId)
            throw new InvalidOperationException("Current policy revision is missing or out of scope.");
        await RequireAuditAsync(revision, ct);
        return revision;
    }

    private async Task RequireAuditAsync(PolicyRevisionDocument revision, CancellationToken ct)
    {
        var count = await _context.Audit.CountDocumentsAsync(Builders<PolicyAuditDocument>.Filter.Where(a =>
            a.TenantId == revision.TenantId && a.LegalEntityId == revision.LegalEntityId
            && a.PolicyId == revision.PolicyId && a.RevisionId == revision.Id), cancellationToken: ct);
        if (count < revision.Version)
            throw new InvalidOperationException("Policy revision audit is incomplete.");
    }

    private static FilterDefinition<PolicyDocument> PolicyFilter(Guid tenantId, Guid legalEntityId)
        => Builders<PolicyDocument>.Filter.Where(p => p.TenantId == tenantId.ToString("D")
            && p.LegalEntityId == legalEntityId.ToString("D") && !p.IsDeleted);

    private static SeriesKey Series(PolicyDocument policy)
        => new(Guid.Parse(policy.SkuId), Guid.Parse(policy.WarehouseId));
}
