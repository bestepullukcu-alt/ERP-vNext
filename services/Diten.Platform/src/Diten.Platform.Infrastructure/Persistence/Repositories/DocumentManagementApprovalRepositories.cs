using Diten.Platform.Infrastructure.Persistence.Schema;
using Diten.Platform.Common.Persistence;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.DocumentManagement;
using Diten.Platform.Domain.Repositories;
using MongoDB.Driver;

namespace Diten.Platform.Infrastructure.Persistence.Repositories;

// MOD-0029-FU09 — tenant-scoped Mongo repositories for approval requirements + immutable evidence. No hard delete.

public sealed class DocumentApprovalRequirementRepository
    : TenantRepository<DocumentApprovalRequirement>, IDocumentApprovalRequirementRepository
{
    public DocumentApprovalRequirementRepository(IPlatformDbContext dbContext, ITenantContext tenantContext)
        : base(dbContext.Database, tenantContext, PlatformCollections.DocumentManagementApprovalRequirements) { }

    public new Task<DocumentApprovalRequirement> CreateAsync(DocumentApprovalRequirement requirement, CancellationToken ct = default) =>
        base.CreateAsync(requirement, ct);

    public async Task<IReadOnlyList<DocumentApprovalRequirement>> GetByRegisterEntryAsync(Guid registerEntryId, CancellationToken ct = default) =>
        await Collection.Find(Builders<DocumentApprovalRequirement>.Filter.And(
                ExecutionFilter, Builders<DocumentApprovalRequirement>.Filter.Eq(x => x.RegisterEntryId, registerEntryId)))
            .SortBy(x => x.CreatedAt).ToListAsync(ct);

    public async Task<bool> UpdateAsync(DocumentApprovalRequirement requirement, CancellationToken ct = default)
    {
        var result = await Collection.ReplaceOneAsync(
            Builders<DocumentApprovalRequirement>.Filter.And(ExecutionFilter,
                Builders<DocumentApprovalRequirement>.Filter.Eq(x => x.Id, requirement.Id)),
            requirement, cancellationToken: ct);
        return result.ModifiedCount > 0;
    }
}

public sealed class DocumentApprovalEvidenceRepository
    : TenantRepository<DocumentApprovalEvidence>, IDocumentApprovalEvidenceRepository
{
    public DocumentApprovalEvidenceRepository(IPlatformDbContext dbContext, ITenantContext tenantContext)
        : base(dbContext.Database, tenantContext, PlatformCollections.DocumentManagementApprovalEvidence) { }

    public new Task<DocumentApprovalEvidence> CreateAsync(DocumentApprovalEvidence evidence, CancellationToken ct = default) =>
        base.CreateAsync(evidence, ct);

    public async Task<IReadOnlyList<DocumentApprovalEvidence>> GetByRegisterEntryAsync(Guid registerEntryId, CancellationToken ct = default) =>
        await Collection.Find(Builders<DocumentApprovalEvidence>.Filter.And(
                ExecutionFilter, Builders<DocumentApprovalEvidence>.Filter.Eq(x => x.RegisterEntryId, registerEntryId)))
            .SortByDescending(x => x.PerformedAt).ToListAsync(ct);

    public async Task<IReadOnlyList<DocumentApprovalEvidence>> GetByRequirementAsync(Guid requirementId, CancellationToken ct = default) =>
        await Collection.Find(Builders<DocumentApprovalEvidence>.Filter.And(
                ExecutionFilter, Builders<DocumentApprovalEvidence>.Filter.Eq(x => x.RequirementId, requirementId)))
            .SortByDescending(x => x.PerformedAt).ToListAsync(ct);
}
