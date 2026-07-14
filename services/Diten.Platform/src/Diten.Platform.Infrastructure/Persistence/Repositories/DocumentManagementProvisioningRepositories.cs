using Diten.Platform.Common.Persistence;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.DocumentManagement;
using Diten.Platform.Domain.Enums.DocumentManagement;
using Diten.Platform.Domain.Repositories;
using MongoDB.Driver;

namespace Diten.Platform.Infrastructure.Persistence.Repositories;

/// <summary>
/// MOD-0028-FU09 tenant-scoped provisioning-evidence persistence (sidecar). All reads/writes are tenant-filtered via
/// <see cref="TenantRepository{TEntity}"/>; there is no hard delete.
/// </summary>
public sealed class ProvisioningEvidenceRepository
    : TenantRepository<DocumentCollectionProvisioningEvidence>, IProvisioningEvidenceRepository
{
    public ProvisioningEvidenceRepository(IPlatformDbContext dbContext, ITenantContext tenantContext)
        : base(dbContext.Database, tenantContext, "document_management_collection_provisioning_evidence")
    {
    }

    public Task<DocumentCollectionProvisioningEvidence?> GetByCollectionInstanceAsync(Guid collectionInstanceId, CancellationToken ct = default)
    {
        var filter = Builders<DocumentCollectionProvisioningEvidence>.Filter.And(
            ExecutionFilter,
            Builders<DocumentCollectionProvisioningEvidence>.Filter.Eq(x => x.CollectionInstanceId, collectionInstanceId));
        return Collection.Find(filter).FirstOrDefaultAsync(ct)!;
    }

    public async Task<IReadOnlyList<DocumentCollectionProvisioningEvidence>> GetByBaselineAsync(Guid baselineReleaseId, CancellationToken ct = default)
    {
        var filter = Builders<DocumentCollectionProvisioningEvidence>.Filter.And(
            ExecutionFilter,
            Builders<DocumentCollectionProvisioningEvidence>.Filter.Eq(x => x.BaselineReleaseId, baselineReleaseId));
        return await Collection.Find(filter).ToListAsync(ct);
    }

    public async Task<bool> UpdateAsync(DocumentCollectionProvisioningEvidence evidence, CancellationToken ct = default)
    {
        evidence.UpdatedAt = DateTimeOffset.UtcNow;
        var filter = Builders<DocumentCollectionProvisioningEvidence>.Filter.And(
            ExecutionFilter,
            Builders<DocumentCollectionProvisioningEvidence>.Filter.Eq(x => x.Id, evidence.Id));
        var result = await Collection.ReplaceOneAsync(filter, evidence, new ReplaceOptions(), ct);
        return result.IsAcknowledged && result.ModifiedCount == 1;
    }
}

/// <summary>MOD-0028-FU09 tenant-scoped deviation persistence (sidecar); soft delete only.</summary>
public sealed class DocumentCollectionDeviationRepository
    : TenantRepository<DocumentCollectionDeviation>, IDocumentCollectionDeviationRepository
{
    public DocumentCollectionDeviationRepository(IPlatformDbContext dbContext, ITenantContext tenantContext)
        : base(dbContext.Database, tenantContext, "document_management_collection_deviations")
    {
    }

    public async Task<IReadOnlyList<DocumentCollectionDeviation>> GetByBaselineAsync(Guid baselineReleaseId, CancellationToken ct = default)
    {
        var filter = Builders<DocumentCollectionDeviation>.Filter.And(
            ExecutionFilter,
            Builders<DocumentCollectionDeviation>.Filter.Eq(x => x.BaselineReleaseId, baselineReleaseId));
        return await Collection.Find(filter).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<DocumentCollectionDeviation>> GetOpenByBaselineAsync(Guid baselineReleaseId, CancellationToken ct = default)
    {
        var filter = Builders<DocumentCollectionDeviation>.Filter.And(
            ExecutionFilter,
            Builders<DocumentCollectionDeviation>.Filter.Eq(x => x.BaselineReleaseId, baselineReleaseId),
            Builders<DocumentCollectionDeviation>.Filter.Eq(x => x.Status, DeviationStatus.Open));
        return await Collection.Find(filter).ToListAsync(ct);
    }

    public async Task<bool> UpdateAsync(DocumentCollectionDeviation deviation, CancellationToken ct = default)
    {
        deviation.UpdatedAt = DateTimeOffset.UtcNow;
        var filter = Builders<DocumentCollectionDeviation>.Filter.And(
            ExecutionFilter,
            Builders<DocumentCollectionDeviation>.Filter.Eq(x => x.Id, deviation.Id));
        var result = await Collection.ReplaceOneAsync(filter, deviation, new ReplaceOptions(), ct);
        return result.IsAcknowledged && result.ModifiedCount == 1;
    }
}
