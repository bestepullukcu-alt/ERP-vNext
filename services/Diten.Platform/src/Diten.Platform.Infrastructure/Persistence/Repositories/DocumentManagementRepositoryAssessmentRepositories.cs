using Diten.Platform.Infrastructure.Persistence.Schema;
using Diten.Platform.Common.Persistence;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.DocumentManagement;
using Diten.Platform.Domain.Repositories;
using MongoDB.Driver;

namespace Diten.Platform.Infrastructure.Persistence.Repositories;

// MOD-0029-FU16 — tenant-scoped Mongo repositories for repository assessments + findings. No hard delete.

public sealed class DocumentRepositoryAssessmentRepository
    : TenantRepository<DocumentRepositoryAssessment>, IDocumentRepositoryAssessmentRepository
{
    public DocumentRepositoryAssessmentRepository(IPlatformDbContext dbContext, ITenantContext tenantContext)
        : base(dbContext.Database, tenantContext, PlatformCollections.DocumentManagementRepositoryAssessments) { }

    public new Task<DocumentRepositoryAssessment> CreateAsync(DocumentRepositoryAssessment assessment, CancellationToken ct = default) =>
        base.CreateAsync(assessment, ct);

    public async Task<IReadOnlyList<DocumentRepositoryAssessment>> GetAllForTenantAsync(CancellationToken ct = default) =>
        await Collection.Find(ExecutionFilter).SortByDescending(x => x.CreatedAt).ToListAsync(ct);

    public async Task<bool> UpdateAsync(DocumentRepositoryAssessment assessment, CancellationToken ct = default)
    {
        var result = await Collection.ReplaceOneAsync(
            Builders<DocumentRepositoryAssessment>.Filter.And(ExecutionFilter,
                Builders<DocumentRepositoryAssessment>.Filter.Eq(x => x.Id, assessment.Id)),
            assessment, cancellationToken: ct);
        return result.ModifiedCount > 0;
    }
}

public sealed class DocumentRepositoryAssessmentFindingRepository
    : TenantRepository<DocumentRepositoryAssessmentFinding>, IDocumentRepositoryAssessmentFindingRepository
{
    public DocumentRepositoryAssessmentFindingRepository(IPlatformDbContext dbContext, ITenantContext tenantContext)
        : base(dbContext.Database, tenantContext, PlatformCollections.DocumentManagementRepositoryAssessmentFindings) { }

    public new Task<DocumentRepositoryAssessmentFinding> CreateAsync(DocumentRepositoryAssessmentFinding finding, CancellationToken ct = default) =>
        base.CreateAsync(finding, ct);

    public async Task<IReadOnlyList<DocumentRepositoryAssessmentFinding>> GetByAssessmentAsync(Guid repositoryAssessmentId, CancellationToken ct = default) =>
        await Collection.Find(Builders<DocumentRepositoryAssessmentFinding>.Filter.And(
                ExecutionFilter, Builders<DocumentRepositoryAssessmentFinding>.Filter.Eq(x => x.RepositoryAssessmentId, repositoryAssessmentId)))
            .SortBy(x => x.CreatedAt).ToListAsync(ct);

    public async Task<bool> UpdateAsync(DocumentRepositoryAssessmentFinding finding, CancellationToken ct = default)
    {
        var result = await Collection.ReplaceOneAsync(
            Builders<DocumentRepositoryAssessmentFinding>.Filter.And(ExecutionFilter,
                Builders<DocumentRepositoryAssessmentFinding>.Filter.Eq(x => x.Id, finding.Id)),
            finding, cancellationToken: ct);
        return result.ModifiedCount > 0;
    }
}
