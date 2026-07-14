using Diten.Platform.Common.Persistence;
using Diten.Platform.Domain.Enums.DocumentManagement;

namespace Diten.Platform.Domain.Entities.DocumentManagement;

/// <summary>
/// MOD-0028-FU09 — IQ provisioning evidence for one collection node, kept as a tenant-scoped SIDECAR aggregate so it
/// never mutates the MOD-0028 <see cref="CollectionDefinition"/>/<see cref="CollectionInstance"/> identity. Records
/// where/when a folder was provisioned (platform ids), the IT permissions sign-off, and the QA verification sign-off.
/// Evidence is additive and non-destructive; the same node's evidence is upserted, and audit fields track changes.
/// </summary>
public sealed class DocumentCollectionProvisioningEvidence : TenantScopedEntity
{
    public required Guid BaselineReleaseId { get; set; }
    public Guid? CollectionDefinitionId { get; set; }
    public required Guid CollectionInstanceId { get; set; }

    public string? RegisterFolderId { get; set; }
    public string? RegisterParentFolderId { get; set; }
    public required string FullPath { get; set; }

    public ProvisioningPlatformProvider PlatformProvider { get; set; } = ProvisioningPlatformProvider.InHouse;
    public string? PlatformFolderId { get; set; }
    public string? PlatformParentId { get; set; }
    public ProvisioningEvidenceStatus ProvisioningStatus { get; set; } = ProvisioningEvidenceStatus.Pending;

    public DateTimeOffset? CreatedOnPlatformAt { get; set; }
    public string? CreatedOnPlatformBy { get; set; }
    public Guid? CreatedByUserId { get; set; }

    // IT permission qualification sign-off.
    public bool PermissionsApplied { get; set; }
    public DateTimeOffset? PermissionsAppliedAt { get; set; }
    public string? PermissionsAppliedBy { get; set; }

    // QA verification sign-off.
    public bool QaVerified { get; set; }
    public DateTimeOffset? QaVerifiedAt { get; set; }
    public string? QaVerifiedBy { get; set; }

    public EvidenceDeviationStatus DeviationStatus { get; set; } = EvidenceDeviationStatus.None;
    public string? DeviationComment { get; set; }

    public DateTimeOffset? LastReadBackAt { get; set; }
    public string? LastReadBackHash { get; set; }

    public string? CorrelationId { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}
