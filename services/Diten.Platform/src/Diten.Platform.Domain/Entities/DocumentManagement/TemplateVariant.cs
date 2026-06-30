using Diten.Platform.Common.Persistence;
using Diten.Platform.Domain.Enums.DocumentManagement;

namespace Diten.Platform.Domain.Entities.DocumentManagement;

/// <summary>
/// MOD-0029-FU03 — tenant-scoped company / business-unit / site governance + drift record derived from a corporate
/// <see cref="TemplateMaster"/>. Intentionally separate from the folder-attached <see cref="TemplateDocument"/>;
/// no binary/content is stored here. Drift status is never persisted — it is computed read-time.
/// </summary>
public sealed class TemplateVariant : TenantScopedEntity
{
    public required Guid TemplateMasterId { get; set; }
    public required Guid TemplateMasterVersionId { get; set; }
    public required string VariantCode { get; set; }
    public required string VariantName { get; set; }
    public string? Description { get; set; }
    public TemplateVariantScopeType ScopeType { get; set; } = TemplateVariantScopeType.Company;
    public required Guid ScopeId { get; set; }
    public Guid? OwnerCompanyId { get; set; }
    public Guid? OwnerUserId { get; set; }
    public TemplateVariantStatus Status { get; set; } = TemplateVariantStatus.Draft;
    public TemplateVariantContentSource ContentSource { get; set; } = TemplateVariantContentSource.MasterVersion;

    // Last master version explicitly rebased into the variant. Business version number is NOT named `Version`
    // because the technical concurrency `Version` is reserved by the base entity.
    public Guid? LastRebasedMasterVersionId { get; set; }
    public int? LastRebasedMasterVersionNumber { get; set; }
    public DateTimeOffset? LastRebasedAt { get; set; }

    // Placeholder pointer for future variant versioning; no TemplateVariantVersion aggregate exists in this FU.
    public Guid? CurrentVariantVersionId { get; set; }

    // Optional folder-attached runtime template link. Rebase must never overwrite linked content.
    public Guid? LinkedTemplateDocumentId { get; set; }

    public bool HasLocalChanges { get; set; }

    // Metadata/read-only approval placeholder. No approval workflow side effects in this FU.
    public TemplateVariantApprovalStatus ApprovalStatus { get; set; } = TemplateVariantApprovalStatus.NotRequired;
    public Guid? ApprovalRequestId { get; set; }
    public string? BlockedReason { get; set; }

    public DateTimeOffset? DeletedAt { get; set; }
}
