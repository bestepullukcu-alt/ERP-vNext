using Diten.Platform.Common.Persistence;
using Diten.Platform.Domain.Enums.DocumentManagement;

namespace Diten.Platform.Domain.Entities.DocumentManagement;

/// <summary>
/// MOD-0029-FU36 — durable, tenant-scoped support evidence for controlled-document registration.
/// Stores references only; never file bytes or public URLs. No hard-delete endpoint/repository method exists.
/// </summary>
public sealed class ControlledDocumentRegistrationOperation : TenantScopedEntity
{
    public required string IdempotencyKey { get; init; }
    public ControlledDocumentRegistrationStatus Status { get; private set; } = ControlledDocumentRegistrationStatus.Pending;
    public Guid? ControlledDocumentId { get; private set; }
    public Guid? ControlledDocumentVersionId { get; private set; }
    public Guid? MasterRegisterEntryId { get; private set; }
    public string? ContentRef { get; private set; }
    public string? ContentSha256 { get; private set; }
    public string? ContentDescriptorJson { get; private set; }
    public string? RegistrationMetadataJson { get; private set; }
    public DocumentScope DocumentScope { get; private set; } = DocumentScope.Company;
    public Guid ScopeOwnerId { get; private set; }
    public Guid CompanyId { get; private set; }
    public Guid OwnerCompanyId { get; private set; }
    public Guid CorporateOwnerId { get; private set; }
    public Guid CollectionInstanceId { get; private set; }
    public Guid FolderId { get; private set; }
    public string? StoragePartition { get; private set; }
    public Guid? BaselineReleaseId { get; private set; }
    public string? CorporateProvisioningReference { get; private set; }
    public string? GoverningLanguageId { get; private set; }
    public string? RetentionClassId { get; private set; }
    public string? GovernanceOwnerFunction { get; private set; }
    public string? ProcessOwnerRole { get; private set; }
    public Guid? ProcessOwnerUserId { get; private set; }
    public string? ScopeFingerprint { get; private set; }
    public string? FailureReasonCode { get; private set; }
    public string? FailureDetail { get; private set; }
    public DateTimeOffset? LastAttemptAt { get; private set; }
    public int AttemptCount { get; private set; } = 1;
    public required string CorrelationId { get; init; }
    public DateTimeOffset? DeletedAt { get; set; }

    public void StartAttempt(string actor)
    {
        LastAttemptAt = DateTimeOffset.UtcNow;
        if (LastAttemptAt != CreatedAt) AttemptCount++;
        Touch(actor);
    }

    public void CaptureRegistrationMetadata(string metadataJson, string actor)
    {
        RegistrationMetadataJson ??= metadataJson;
        Touch(actor);
    }

    public bool CaptureScopeSnapshot(
        DocumentScope documentScope,
        Guid scopeOwnerId,
        Guid companyId,
        Guid ownerCompanyId,
        Guid corporateOwnerId,
        Guid collectionInstanceId,
        Guid folderId,
        string storagePartition,
        Guid? baselineReleaseId,
        string? corporateProvisioningReference,
        string governingLanguageId,
        string retentionClassId,
        string? governanceOwnerFunction,
        string? processOwnerRole,
        Guid? processOwnerUserId,
        string fingerprint,
        string actor)
    {
        if (ScopeFingerprint is not null)
        {
            return string.Equals(ScopeFingerprint, fingerprint, StringComparison.Ordinal);
        }

        DocumentScope = documentScope;
        ScopeOwnerId = scopeOwnerId;
        CompanyId = companyId;
        OwnerCompanyId = ownerCompanyId;
        CorporateOwnerId = corporateOwnerId;
        CollectionInstanceId = collectionInstanceId;
        FolderId = folderId;
        StoragePartition = storagePartition;
        BaselineReleaseId = baselineReleaseId;
        CorporateProvisioningReference = corporateProvisioningReference;
        GoverningLanguageId = governingLanguageId;
        RetentionClassId = retentionClassId;
        GovernanceOwnerFunction = governanceOwnerFunction;
        ProcessOwnerRole = processOwnerRole;
        ProcessOwnerUserId = processOwnerUserId;
        ScopeFingerprint = fingerprint;
        Touch(actor);
        return true;
    }

    public void MarkContentStored(string contentRef, string checksum, string contentDescriptorJson, string actor)
    {
        ContentRef ??= contentRef;
        ContentSha256 ??= checksum;
        ContentDescriptorJson ??= contentDescriptorJson;
        TransitionTo(ControlledDocumentRegistrationStatus.ContentStored, actor);
    }

    public void MarkDocumentCreated(Guid documentId, Guid versionId, string actor)
    {
        ControlledDocumentId ??= documentId;
        ControlledDocumentVersionId ??= versionId;
        TransitionTo(ControlledDocumentRegistrationStatus.DocumentCreated, actor);
    }

    public void MarkRegisterCreated(Guid registerId, string actor)
    {
        MasterRegisterEntryId ??= registerId;
        TransitionTo(ControlledDocumentRegistrationStatus.RegisterCreated, actor);
    }

    public void MarkLinked(string actor) => TransitionTo(ControlledDocumentRegistrationStatus.Linked, actor);
    public void MarkCompleted(string actor) => TransitionTo(ControlledDocumentRegistrationStatus.Completed, actor);

    public void ResetStoredContentAfterCleanup(string actor)
    {
        ContentRef = null;
        ContentSha256 = null;
        ContentDescriptorJson = null;
        Status = ControlledDocumentRegistrationStatus.Pending;
        Touch(actor);
    }

    public void MarkFailure(string reasonCode, string sanitizedDetail, bool compensationPending, string actor)
    {
        FailureReasonCode = reasonCode;
        FailureDetail = sanitizedDetail;
        TransitionTo(
            compensationPending ? ControlledDocumentRegistrationStatus.CompensationPending : ControlledDocumentRegistrationStatus.Failed,
            actor);
    }

    private void TransitionTo(ControlledDocumentRegistrationStatus status, string actor)
    {
        Status = status;
        FailureReasonCode = status is ControlledDocumentRegistrationStatus.Completed ? null : FailureReasonCode;
        FailureDetail = status is ControlledDocumentRegistrationStatus.Completed ? null : FailureDetail;
        Touch(actor);
    }

    private void Touch(string actor)
    {
        UpdatedAt = DateTimeOffset.UtcNow;
        UpdatedBy = actor;
        Version++;
    }
}
