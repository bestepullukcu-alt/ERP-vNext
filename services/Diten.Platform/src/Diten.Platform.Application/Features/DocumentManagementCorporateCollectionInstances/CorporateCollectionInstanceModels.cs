using Diten.Platform.Domain.Enums.DocumentManagement;

namespace Diten.Platform.Application.Features.DocumentManagementCorporateCollectionInstances;

public static class CorporateCollectionInstanceReasonCodes
{
    public const string NotFoundNonLeakage = "NOT_FOUND_NON_LEAKAGE";
    public const string ValidationFailed = "VALIDATION_FAILED";
    public const string BaselineNotEligible = "BASELINE_NOT_ELIGIBLE";
    public const string Forbidden = "FORBIDDEN";
    public const string ProvisioningFailed = "PROVISIONING_FAILED";
}

public sealed record CorporateCollectionProvisioningResult(
    Guid OperationId,
    Guid CollectionInstanceId,
    Guid BaselineReleaseId,
    Guid CorporateOwnerId,
    string ScopeType,
    string Status,
    int FolderCount,
    bool IdempotentReplay,
    string CorrelationId);

public sealed record CorporateCollectionInstanceModel(
    Guid Id,
    string InstanceKey,
    Guid BaselineReleaseId,
    Guid ScopeOwnerId,
    Guid CorporateOwnerId,
    string CanonicalId,
    string? ParentCanonicalId,
    string Name,
    string FullPath,
    string Status,
    string StoragePartition);

public sealed record CorporateCollectionProvisioningOperationModel(
    Guid Id,
    Guid BaselineReleaseId,
    Guid CorporateOwnerId,
    string Status,
    Guid? CollectionInstanceId,
    int AttemptCount,
    DateTimeOffset LastAttemptAt,
    string? FailureReasonCode,
    string CorrelationId);

internal static class CorporateCollectionMapping
{
    public static CorporateCollectionInstanceModel ToModel(Domain.Entities.DocumentManagement.CollectionInstance x) =>
        new(x.Id, x.InstanceKey, x.BaselineReleaseId, x.ScopeOwnerId, x.CorporateOwnerId, x.CanonicalId,
            x.ParentCanonicalId, x.Name, x.FullPath, x.InstanceStatus.ToString().ToUpperInvariant(), x.StoragePartition ?? string.Empty);

    public static CorporateCollectionProvisioningOperationModel ToModel(
        Domain.Entities.DocumentManagement.CorporateCollectionInstanceProvisioningOperation x) =>
        new(x.Id, x.BaselineReleaseId, x.CorporateOwnerId, x.Status.ToString().ToUpperInvariant(),
            x.CollectionInstanceId, x.AttemptCount, x.LastAttemptAt, x.FailureReasonCode, x.CorrelationId);
}
