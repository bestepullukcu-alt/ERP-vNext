using Diten.Platform.Domain.Entities.DocumentManagement;

namespace Diten.Platform.Domain.Repositories;

// MOD-0029-FU18 — variant localization profile / review evidence / parent change assessment contracts.
// Tenant-scoped via the TenantRepository ExecutionFilter. There is deliberately NO delete method: evidence is
// append-only and assessment history is preserved so the parent-linkage trail can never be lost.

public interface ITemplateVariantLocalizationProfileRepository
{
    Task<TemplateVariantLocalizationProfile> CreateAsync(TemplateVariantLocalizationProfile profile, CancellationToken ct = default);
    Task<TemplateVariantLocalizationProfile?> GetByVariantAsync(Guid templateVariantId, CancellationToken ct = default);
    Task<IReadOnlyList<TemplateVariantLocalizationProfile>> GetByParentMasterAsync(Guid parentTemplateMasterId, CancellationToken ct = default);
    Task<IReadOnlyList<TemplateVariantLocalizationProfile>> GetAllForTenantAsync(CancellationToken ct = default);
    Task<bool> UpdateAsync(TemplateVariantLocalizationProfile profile, CancellationToken ct = default);
}

public interface ITemplateVariantReviewEvidenceRepository
{
    Task<TemplateVariantReviewEvidence> CreateAsync(TemplateVariantReviewEvidence evidence, CancellationToken ct = default);
    Task<IReadOnlyList<TemplateVariantReviewEvidence>> GetByVariantAsync(Guid templateVariantId, CancellationToken ct = default);
}

public interface ITemplateVariantParentChangeAssessmentRepository
{
    Task<TemplateVariantParentChangeAssessment> CreateAsync(TemplateVariantParentChangeAssessment assessment, CancellationToken ct = default);
    Task<IReadOnlyList<TemplateVariantParentChangeAssessment>> GetByVariantAsync(Guid templateVariantId, CancellationToken ct = default);
    Task<TemplateVariantParentChangeAssessment?> GetLatestAsync(Guid templateVariantId, CancellationToken ct = default);
}
