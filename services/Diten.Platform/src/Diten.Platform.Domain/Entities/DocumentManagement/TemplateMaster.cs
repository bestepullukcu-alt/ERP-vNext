using Diten.Platform.Common.Persistence;
using Diten.Platform.Domain.Enums.DocumentManagement;

namespace Diten.Platform.Domain.Entities.DocumentManagement;

/// <summary>
/// MOD-0029-FU02 — tenant/corporate reusable master template definition. This is intentionally separate
/// from folder-attached <see cref="TemplateDocument"/>.
/// </summary>
public sealed class TemplateMaster : TenantScopedEntity
{
    public required string MasterCode { get; set; }
    public required string TemplateName { get; set; }
    public string? Description { get; set; }
    public required string Classification { get; set; }
    public Guid? CollectionDefinitionId { get; set; }
    public string? CanonicalId { get; set; }
    public TemplateVariantPolicy VariantPolicy { get; set; } = TemplateVariantPolicy.Allowed;
    public Guid? OwnerCompanyId { get; set; }
    public Guid? OwnerUserId { get; set; }
    public TemplateMasterStatus Status { get; set; } = TemplateMasterStatus.Draft;
    public Guid? CurrentVersionId { get; set; }
    public int CurrentMasterVersion { get; set; }
    public DateTimeOffset? EffectiveDate { get; set; }
    public DateTimeOffset? DeprecatedAt { get; set; }
    public string? DeprecatedBy { get; set; }
    public int? UsageCount { get; set; }
    public int? VariantCount { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}
