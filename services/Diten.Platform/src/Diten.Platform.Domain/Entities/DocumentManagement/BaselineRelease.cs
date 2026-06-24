using Diten.Platform.Common.Persistence;
using Diten.Platform.Domain.Enums.DocumentManagement;

namespace Diten.Platform.Domain.Entities.DocumentManagement;

/// <summary>
/// MOD-0028-FU02 — a draft/published baseline for an imported QMS folder tree. Only DRAFT may publish;
/// only PUBLISHED is later instantiable (instantiation is out of FU02 scope).
/// </summary>
public sealed class BaselineRelease : TenantScopedEntity
{
    public required string BaselineReleaseId { get; set; }

    /// <summary>Stable source key for the imported workbook/baseline; part of the deterministic canonical key.</summary>
    public required string SourceBaselineKey { get; set; }

    /// <summary>Semantic business version (never the inherited technical <see cref="Common.Persistence.BaseEntity.Version"/>).</summary>
    public required string BaselineVersion { get; set; }

    public DateTimeOffset? EffectiveDate { get; set; }
    public BaselineReleaseStatus Status { get; set; } = BaselineReleaseStatus.Draft;
    public string? ChangeSummary { get; set; }

    /// <summary>Deterministic structural hash; set on publish.</summary>
    public string? SnapshotHash { get; set; }

    /// <summary>Id of the immutable manifest produced on publish.</summary>
    public Guid? ManifestId { get; set; }

    public int DeprecationNoticeWindowDays { get; set; }

    public DateTimeOffset? PublishedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}
