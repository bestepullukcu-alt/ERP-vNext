using Diten.Platform.Common.Persistence;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.DocumentManagement;
using Diten.Platform.Domain.Repositories;
using MongoDB.Driver;

namespace Diten.Platform.Infrastructure.Persistence.Repositories;

// MOD-0029-FU18 — tenant-scoped Mongo repositories for variant localization profiles, review evidence and parent
// change assessments. No delete operation on any of them; no document content is ever persisted here.

public sealed class TemplateVariantLocalizationProfileRepository
    : TenantRepository<TemplateVariantLocalizationProfile>, ITemplateVariantLocalizationProfileRepository
{
    public TemplateVariantLocalizationProfileRepository(IPlatformDbContext dbContext, ITenantContext tenantContext)
        : base(dbContext.Database, tenantContext, "document_management_variant_localization_profiles") { }

    public new Task<TemplateVariantLocalizationProfile> CreateAsync(TemplateVariantLocalizationProfile profile, CancellationToken ct = default) =>
        base.CreateAsync(profile, ct);

    public async Task<TemplateVariantLocalizationProfile?> GetByVariantAsync(Guid templateVariantId, CancellationToken ct = default) =>
        await Collection.Find(Builders<TemplateVariantLocalizationProfile>.Filter.And(
                ExecutionFilter,
                Builders<TemplateVariantLocalizationProfile>.Filter.Eq(x => x.TemplateVariantId, templateVariantId)))
            .FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<TemplateVariantLocalizationProfile>> GetByParentMasterAsync(Guid parentTemplateMasterId, CancellationToken ct = default) =>
        await Collection.Find(Builders<TemplateVariantLocalizationProfile>.Filter.And(
                ExecutionFilter,
                Builders<TemplateVariantLocalizationProfile>.Filter.Eq(x => x.ParentTemplateMasterId, parentTemplateMasterId)))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<TemplateVariantLocalizationProfile>> GetAllForTenantAsync(CancellationToken ct = default) =>
        await Collection.Find(ExecutionFilter).SortByDescending(x => x.CreatedAt).ToListAsync(ct);

    public async Task<bool> UpdateAsync(TemplateVariantLocalizationProfile profile, CancellationToken ct = default)
    {
        var result = await Collection.ReplaceOneAsync(
            Builders<TemplateVariantLocalizationProfile>.Filter.And(ExecutionFilter,
                Builders<TemplateVariantLocalizationProfile>.Filter.Eq(x => x.Id, profile.Id)),
            profile, cancellationToken: ct);
        return result.ModifiedCount > 0;
    }
}

public sealed class TemplateVariantReviewEvidenceRepository
    : TenantRepository<TemplateVariantReviewEvidence>, ITemplateVariantReviewEvidenceRepository
{
    public TemplateVariantReviewEvidenceRepository(IPlatformDbContext dbContext, ITenantContext tenantContext)
        : base(dbContext.Database, tenantContext, "document_management_variant_review_evidence") { }

    public new Task<TemplateVariantReviewEvidence> CreateAsync(TemplateVariantReviewEvidence evidence, CancellationToken ct = default) =>
        base.CreateAsync(evidence, ct);

    public async Task<IReadOnlyList<TemplateVariantReviewEvidence>> GetByVariantAsync(Guid templateVariantId, CancellationToken ct = default) =>
        await Collection.Find(Builders<TemplateVariantReviewEvidence>.Filter.And(
                ExecutionFilter,
                Builders<TemplateVariantReviewEvidence>.Filter.Eq(x => x.TemplateVariantId, templateVariantId)))
            .SortBy(x => x.CreatedAt).ToListAsync(ct);
}

public sealed class TemplateVariantParentChangeAssessmentRepository
    : TenantRepository<TemplateVariantParentChangeAssessment>, ITemplateVariantParentChangeAssessmentRepository
{
    public TemplateVariantParentChangeAssessmentRepository(IPlatformDbContext dbContext, ITenantContext tenantContext)
        : base(dbContext.Database, tenantContext, "document_management_variant_parent_change_assessments") { }

    public new Task<TemplateVariantParentChangeAssessment> CreateAsync(TemplateVariantParentChangeAssessment assessment, CancellationToken ct = default) =>
        base.CreateAsync(assessment, ct);

    public async Task<IReadOnlyList<TemplateVariantParentChangeAssessment>> GetByVariantAsync(Guid templateVariantId, CancellationToken ct = default) =>
        await Collection.Find(Builders<TemplateVariantParentChangeAssessment>.Filter.And(
                ExecutionFilter,
                Builders<TemplateVariantParentChangeAssessment>.Filter.Eq(x => x.TemplateVariantId, templateVariantId)))
            .SortByDescending(x => x.AssessedAt).ToListAsync(ct);

    public async Task<TemplateVariantParentChangeAssessment?> GetLatestAsync(Guid templateVariantId, CancellationToken ct = default) =>
        await Collection.Find(Builders<TemplateVariantParentChangeAssessment>.Filter.And(
                ExecutionFilter,
                Builders<TemplateVariantParentChangeAssessment>.Filter.Eq(x => x.TemplateVariantId, templateVariantId)))
            .SortByDescending(x => x.AssessedAt).FirstOrDefaultAsync(ct);
}
