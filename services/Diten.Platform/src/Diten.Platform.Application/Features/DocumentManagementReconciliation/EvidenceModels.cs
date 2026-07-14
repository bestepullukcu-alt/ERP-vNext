using Diten.Platform.Domain.Enums.DocumentManagement;

namespace Diten.Platform.Application.Features.DocumentManagementReconciliation;

public sealed record EvidenceUpsertInput(
    Guid BaselineReleaseId,
    Guid CollectionInstanceId,
    Guid? CollectionDefinitionId,
    string? RegisterFolderId,
    string? RegisterParentFolderId,
    string FullPath,
    ProvisioningPlatformProvider PlatformProvider,
    string? PlatformFolderId,
    string? PlatformParentId,
    ProvisioningEvidenceStatus? ProvisioningStatus,
    DateTimeOffset? CreatedOnPlatformAt,
    string? CreatedOnPlatformBy,
    string? DeviationComment);

public sealed record ProvisioningEvidenceModel(
    Guid Id,
    Guid BaselineReleaseId,
    Guid CollectionInstanceId,
    Guid? CollectionDefinitionId,
    string? RegisterFolderId,
    string FullPath,
    string PlatformProvider,
    string? PlatformFolderId,
    string? PlatformParentId,
    string ProvisioningStatus,
    DateTimeOffset? CreatedOnPlatformAt,
    string? CreatedOnPlatformBy,
    bool PermissionsApplied,
    DateTimeOffset? PermissionsAppliedAt,
    string? PermissionsAppliedBy,
    bool QaVerified,
    DateTimeOffset? QaVerifiedAt,
    string? QaVerifiedBy,
    string DeviationStatus,
    string? DeviationComment,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

public sealed record DeviationModel(
    Guid Id,
    Guid BaselineReleaseId,
    Guid? CollectionInstanceId,
    string? RegisterFolderId,
    string ExpectedFullPath,
    string? ActualFullPath,
    string DeviationType,
    string Severity,
    string Status,
    string? Description,
    string? ResolutionComment,
    DateTimeOffset DetectedAt,
    string? DetectedBy,
    DateTimeOffset? ResolvedAt,
    string? ResolvedBy);

public sealed record QualificationReadinessModel(
    Guid BaselineReleaseId,
    string BaselineStatus,
    bool Ready,
    int ExpectedInstanceCount,
    int EvidenceCount,
    int MissingEvidenceCount,
    int PermissionsAppliedCount,
    int QaVerifiedCount,
    int OpenBlockingDeviationCount,
    IReadOnlyList<string> Reasons);

public static class ReconciliationMapping
{
    public static ProvisioningEvidenceModel ToModel(Domain.Entities.DocumentManagement.DocumentCollectionProvisioningEvidence e) => new(
        e.Id, e.BaselineReleaseId, e.CollectionInstanceId, e.CollectionDefinitionId, e.RegisterFolderId, e.FullPath,
        e.PlatformProvider.ToString(), e.PlatformFolderId, e.PlatformParentId, e.ProvisioningStatus.ToString(),
        e.CreatedOnPlatformAt, e.CreatedOnPlatformBy, e.PermissionsApplied, e.PermissionsAppliedAt, e.PermissionsAppliedBy,
        e.QaVerified, e.QaVerifiedAt, e.QaVerifiedBy, e.DeviationStatus.ToString(), e.DeviationComment,
        e.CreatedAt, e.UpdatedAt);

    public static DeviationModel ToModel(Domain.Entities.DocumentManagement.DocumentCollectionDeviation d) => new(
        d.Id, d.BaselineReleaseId, d.CollectionInstanceId, d.RegisterFolderId, d.ExpectedFullPath, d.ActualFullPath,
        d.DeviationType.ToString(), d.Severity.ToString(), d.Status.ToString(), d.Description, d.ResolutionComment,
        d.DetectedAt, d.DetectedBy, d.ResolvedAt, d.ResolvedBy);
}
