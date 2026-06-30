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

    public DateTimeOffset? DeletedAt { get; set; }
}
