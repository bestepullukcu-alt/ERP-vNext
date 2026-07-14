using Diten.Platform.Common.Persistence;
using Diten.Platform.Domain.Enums.DocumentManagement;

namespace Diten.Platform.Domain.Entities.DocumentManagement;

/// <summary>
/// MOD-0029-FU04 — generalized, audit-ready, tenant-scoped resource access policy row (a sidecar collection). It is
/// intentionally separate from the FU01 embedded <see cref="DocumentAccessPolicy"/> value object and from the
/// folder-keyed <see cref="FolderDocumentAccessPolicy"/>; this aggregate carries
/// <c>TargetType + TargetId + PrincipalType + PrincipalId + Actions + Effect</c> with inheritance + deny precedence.
/// It never mutates folder hierarchy or MOD-0028 structures. Effective access (incl. inheritance) is computed at
/// read time; <c>IsInherited</c> is a read-model flag only and is not persisted here.
/// </summary>
public sealed class DocumentAccessPolicyEntry : TenantScopedEntity
{
    /// <summary>Stable policy identifier. Defaults to the entity <see cref="Common.Persistence.BaseEntity.Id"/>.</summary>
    public Guid AccessPolicyId { get; set; }

    public DocumentAccessTargetType TargetType { get; set; }
    public required string TargetId { get; set; }

    public DocumentAccessPrincipalType PrincipalType { get; set; }
    public required string PrincipalId { get; set; }

    public List<DocumentAccessMatrixAction> Actions { get; set; } = [];
    public DocumentAccessEffect Effect { get; set; } = DocumentAccessEffect.Allow;

    public bool InheritFromParent { get; set; } = true;

    /// <summary>Parent/source policy for audit trace of materialized inherited rows; computed inheritance is preferred.</summary>
    public Guid? SourcePolicyId { get; set; }

    public DateTimeOffset? ValidFrom { get; set; }
    public DateTimeOffset? ValidTo { get; set; }

    public DocumentAccessPolicyStatus Status { get; set; } = DocumentAccessPolicyStatus.Active;
    public string? Reason { get; set; }
    public string? CorrelationId { get; set; }

    // ── MOD-0029-FU05 — access-profile template provenance (all additive; default Manual for legacy rows). ──
    // Purely descriptive: the resolver never reads these. They let the template engine tell its own generated
    // rows apart from manual ones so it stays idempotent and never overwrites a manually authored policy.
    public DocumentAccessPolicySource PolicySource { get; set; } = DocumentAccessPolicySource.Manual;

    /// <summary>Access profile that produced this row (e.g. "GQMS-Controlled"). Null for manual rows.</summary>
    public string? PolicyTemplateKey { get; set; }

    public Guid? SourceBaselineReleaseId { get; set; }
    public Guid? SourceCollectionDefinitionId { get; set; }
    public Guid? SourceCollectionInstanceId { get; set; }
    public string? SourceRegisterFolderId { get; set; }
    public DateTimeOffset? GeneratedAt { get; set; }
    public string? GeneratedBy { get; set; }

    public DateTimeOffset? DeletedAt { get; set; }
}
