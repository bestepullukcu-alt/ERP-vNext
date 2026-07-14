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

    // ── MOD-0028-FU08 — approval & effective lifecycle (all additive/nullable; no backfill). ──────────

    /// <summary>Source register/package status (e.g. "Draft — do not execute until approved"). Gates MarkEffective.</summary>
    public string? SourcePackageStatus { get; set; }

    public DateTimeOffset? ApprovedAt { get; set; }
    public string? ApprovedBy { get; set; }

    /// <summary>Register approval reference (Cover approval / Change Log Approval Ref).</summary>
    public string? ApprovalReference { get; set; }
    public string? ApprovalComment { get; set; }

    public DateTimeOffset? EffectiveAt { get; set; }
    public string? EffectiveBy { get; set; }

    /// <summary>When this baseline became Superseded by a newer Effective one.</summary>
    public DateTimeOffset? SupersededAt { get; set; }

    /// <summary>The Effective baseline this one replaced (set on the new baseline when it goes Effective).</summary>
    public Guid? SupersedesBaselineReleaseId { get; set; }

    /// <summary>The newer Effective baseline that replaced this one (set on the old baseline when superseded).</summary>
    public Guid? SupersededByBaselineReleaseId { get; set; }

    public DateTimeOffset? DeletedAt { get; set; }
}
