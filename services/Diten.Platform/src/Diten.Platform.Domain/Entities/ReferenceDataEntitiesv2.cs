using Diten.Platform.Common.Persistence;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Diten.Platform.Domain.Entities;

[BsonIgnoreExtraElements]
public sealed class BusinessReferenceDataSet : TenantScopedEntity
{
    [BsonRepresentation(BsonType.String)]
    public Guid BusinessReferenceDataSetId { get; set; } = Guid.NewGuid();

    public required string SetCode { get; set; }
    public required string Name { get; set; }
    public required string ScopeType { get; set; }
    public string? Description { get; set; }
    public BusinessReferenceDataSetStatus Status { get; set; } = BusinessReferenceDataSetStatus.Draft;

    [BsonRepresentation(BsonType.String)]
    public Guid? ActiveDraftVersionId { get; set; }

    [BsonRepresentation(BsonType.String)]
    public Guid? PublishedVersionId { get; set; }

    public long RowVersion { get; set; } = 1;
    public string? LastCorrelationId { get; set; }
    public int UsageRegistrationCount { get; set; }
    public int CriticalUsageCount { get; set; }
    public DateTimeOffset? LastUsageRegisteredAt { get; set; }
}

[BsonIgnoreExtraElements]
public sealed class BusinessReferenceDataVersion : TenantScopedEntity
{
    [BsonRepresentation(BsonType.String)]
    public Guid BusinessReferenceDataVersionId { get; set; } = Guid.NewGuid();

    [BsonRepresentation(BsonType.String)]
    public Guid BusinessReferenceDataSetId { get; set; }

    public int VersionNumber { get; set; }
    public BusinessReferenceDataVersionStatus Status { get; set; } = BusinessReferenceDataVersionStatus.Draft;
    public string ConcurrencyToken { get; set; } = Guid.NewGuid().ToString("N");
    public bool IsImmutable { get; set; }
    public DateTimeOffset? PublishedAt { get; set; }
    public string? PublishedBy { get; set; }

    [BsonRepresentation(BsonType.String)]
    public Guid? SourceVersionId { get; set; }

    [BsonRepresentation(BsonType.String)]
    public Guid? TargetDraftVersionId { get; set; }

    public string? CopyActor { get; set; }
    public DateTimeOffset? CopiedAt { get; set; }
    public string? LastCorrelationId { get; set; }

    public bool RequiresEvidence { get; set; }
    public bool EvidenceAttached { get; set; }
    public bool RequiresApproval { get; set; }
    public DateTimeOffset? ApprovedAt { get; set; }
    public DateTimeOffset? PublishWindowStart { get; set; }
    public DateTimeOffset? PublishWindowEnd { get; set; }
    public BusinessReferenceDataGovernanceState BusinessReferenceDataGovernanceState { get; set; } = BusinessReferenceDataGovernanceState.Draft;
    public BusinessReferenceDataApprovalState BusinessReferenceDataApprovalState { get; set; } = BusinessReferenceDataApprovalState.NotStarted;
    public bool IsEditable { get; set; } = true;
    public DateTimeOffset? SubmittedAt { get; set; }
    public string? SubmittedBy { get; set; }
    public DateTimeOffset? DecisionAt { get; set; }
    public string? DecisionBy { get; set; }
    public string? RejectionReason { get; set; }
    public string? WorkflowTemplateCode { get; set; }

    [BsonRepresentation(BsonType.String)]
    public Guid? WorkflowInstanceId { get; set; }

    public string? WorkflowState { get; set; }
    public bool IsOverrideAction { get; set; }
    public string? OverrideReason { get; set; }
    public string? LastEvidenceRef { get; set; }
    public string? EvidenceLinkId { get; set; }

    [BsonRepresentation(BsonType.String)]
    public Guid? EvidenceEvaluationId { get; set; }

    public string? EvidenceDocumentVersionId { get; set; }
    public string? EvidenceRequirementCode { get; set; }
    public string? EvidenceDecisionCode { get; set; }
    public string? EvidenceReasonCode { get; set; }
    public string? LastPublishIdempotencyKey { get; set; }
    public string? LastPublishMode { get; set; }
    public string? PublishedSnapshotJson { get; set; }
    public int DeprecatedValuesEffectiveCount { get; set; }
    public DateTimeOffset? EffectiveFrom { get; set; }
    public DateTimeOffset? EffectiveTo { get; set; }
    public string? ScopeKey { get; set; }
    public List<BusinessReferenceDataValue> Values { get; set; } = [];
    public List<BusinessReferenceDataAttributeDefinition> AttributeDefinitions { get; set; } = [];
    public List<BusinessReferenceDataMapping> Mappings { get; set; } = [];

    [BsonRepresentation(BsonType.String)]
    public Guid? SupersededByVersionId { get; set; }
}

[BsonIgnoreExtraElements]
public sealed class BusinessReferenceDataValidationResult : TenantScopedEntity
{
    [BsonRepresentation(BsonType.String)]
    public Guid ValidationResultId { get; set; } = Guid.NewGuid();

    [BsonRepresentation(BsonType.String)]
    public Guid BusinessReferenceDataVersionId { get; set; }

    public required string RuleId { get; set; }
    public BusinessReferenceDataValidationSeverity Severity { get; set; } = BusinessReferenceDataValidationSeverity.Info;
    public bool IsBlocking { get; set; }
    public required string Message { get; set; }
    public bool IsStubbed { get; set; }
    public string? StubReason { get; set; }
    public DateTimeOffset ExecutedAt { get; set; } = DateTimeOffset.UtcNow;
    public string? CorrelationId { get; set; }
}

public enum BusinessReferenceDataSetStatus
{
    Draft,
    Active,
    Retired
}

public enum BusinessReferenceDataVersionStatus
{
    Draft,
    Published,
    Deprecated,
    Retired
}

public enum BusinessReferenceDataValidationSeverity
{
    Error,
    Warning,
    Info
}

public enum BusinessReferenceDataGovernanceState
{
    Draft,
    Submitted,
    InReview,
    Approved,
    Rejected
}

public enum BusinessReferenceDataApprovalState
{
    NotStarted,
    Pending,
    Approved,
    Rejected
}

public enum BusinessReferenceDataImportOperation
{
    Insert,
    Update,
    Deprecate,
    NoOp
}

[BsonIgnoreExtraElements]
public sealed class BusinessReferenceDataValue
{
    public required string ValueCode { get; set; }
    public required string DisplayName { get; set; }
    public string? Description { get; set; }
    public bool IsDeprecated { get; set; }

    [BsonIgnore]
    public bool IsActive
    {
        get => !IsDeprecated;
        set => IsDeprecated = !value;
    }

    public string? ReplacementValueCode { get; set; }
    public string? ParentValueCode { get; set; }
    public int SortOrder { get; set; }
    public DateTimeOffset? EffectiveFrom { get; set; }
    public DateTimeOffset? EffectiveTo { get; set; }
    public Dictionary<string, string>? Attributes { get; set; }
}

[BsonIgnoreExtraElements]
public sealed class BusinessReferenceDataAttributeDefinition
{
    public required string AttributeCode { get; set; }
    public required string DisplayName { get; set; }
    public string DataType { get; set; } = "string";
    public bool IsRequired { get; set; }
}

[BsonIgnoreExtraElements]
public sealed class BusinessReferenceDataMapping
{
    public required string MappingKey { get; set; }
    public required string SourceValueCode { get; set; }
    public required string TargetCode { get; set; }
    public string? TargetLabel { get; set; }
}

[BsonIgnoreExtraElements]
public sealed class BusinessReferenceDataIntegrationEvent : TenantScopedEntity
{
    [BsonRepresentation(BsonType.String)]
    public Guid IntegrationEventId { get; set; } = Guid.NewGuid();

    [BsonRepresentation(BsonType.String)]
    public Guid BusinessReferenceDataVersionId { get; set; }

    public required string EventName { get; set; }
    public int EventVersion { get; set; } = 1;
    public required string IdempotencyKey { get; set; }
    public required string PayloadJson { get; set; }
    public DateTimeOffset EmittedAt { get; set; } = DateTimeOffset.UtcNow;
}

[BsonIgnoreExtraElements]
public sealed class BusinessReferenceDataUsageRegistration : TenantScopedEntity
{
    [BsonRepresentation(BsonType.String)]
    public Guid UsageRegistrationId { get; set; } = Guid.NewGuid();

    public required string SetCode { get; set; }
    public required string ConsumerModule { get; set; }
    public required string ConsumerName { get; set; }
    public string? ConsumerEndpoint { get; set; }
    public string? ScopeType { get; set; }
    public string? ScopeKey { get; set; }
    public int? VersionPin { get; set; }
    public DateTimeOffset? AsOfDate { get; set; }
    public string ResolutionMode { get; set; } = "latest";
    public string Criticality { get; set; } = "medium";
    public string? Notes { get; set; }

    [BsonRepresentation(BsonType.String)]
    public Guid? LastResolvedVersionId { get; set; }

    public DateTimeOffset? LastResolvedAt { get; set; }
    public bool IsActive { get; set; } = true;
}

[BsonIgnoreExtraElements]
public sealed class BusinessReferenceDataTenantAssignment : TenantScopedEntity
{
    [BsonRepresentation(BsonType.String)]
    public Guid BusinessReferenceDataTenantAssignmentId { get; set; } = Guid.NewGuid();

    [BsonRepresentation(BsonType.String)]
    public required Guid ConsumerTenantId { get; init; }

    public required string SetCode { get; set; }

    [BsonRepresentation(BsonType.String)]
    public BusinessReferenceDataTenantAssignmentStatus AssignmentStatus { get; set; } = BusinessReferenceDataTenantAssignmentStatus.ACTIVE;

    public DateTimeOffset? RevokedAt { get; set; }
    public string? RevokedBy { get; set; }
}

public enum BusinessReferenceDataTenantAssignmentStatus
{
    ACTIVE,
    REVOKED
}

[BsonIgnoreExtraElements]
public sealed class BusinessReferenceDataPublishOperation : TenantScopedEntity
{
    [BsonRepresentation(BsonType.String)]
    public Guid PublishOperationId { get; set; } = Guid.NewGuid();

    [BsonRepresentation(BsonType.String)]
    public required Guid BusinessReferenceDataSetId { get; init; }

    [BsonRepresentation(BsonType.String)]
    public required Guid BusinessReferenceDataVersionId { get; init; }

    public required string IdempotencyKey { get; set; }

    [BsonRepresentation(BsonType.String)]
    public BusinessReferenceDataPublishOperationState OperationState { get; set; } = BusinessReferenceDataPublishOperationState.PENDING;

    [BsonRepresentation(BsonType.String)]
    public BusinessReferenceDataPublishCheckpoint PublishCheckpoint { get; set; } = BusinessReferenceDataPublishCheckpoint.INITIALIZED;

    [BsonRepresentation(BsonType.String)]
    public Guid? ExpectedPublishedVersionId { get; init; }

    public required long ExpectedSetVersion { get; init; }
    public required string ExpectedTargetVersionToken { get; init; }
    public string? CatalogVersion { get; init; }
    public string? CatalogFingerprint { get; init; }
    public DateTimeOffset? PreMutationContextVerifiedAt { get; set; }
    public int RetryCount { get; set; }
    public DateTimeOffset LastAttemptAt { get; set; } = DateTimeOffset.UtcNow;
    public string? LastErrorCode { get; set; }
    public DateTimeOffset? LastErrorAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
}

public enum BusinessReferenceDataPublishOperationState
{
    PENDING,
    RUNNING,
    RECOVERY_REQUIRED,
    COMPLETED,
    FAILED_TERMINAL
}

public enum BusinessReferenceDataPublishCheckpoint
{
    INITIALIZED,
    TARGET_VERSION_WRITTEN,
    PRIOR_VERSIONS_DEPRECATED,
    REQUIRED_WRITES_VERIFIED,
    POINTER_PROMOTED,
    COMPLETION_VERIFIED
}

public static class BusinessReferenceDataPublishStateMachine
{
    public static bool IsValidTransition(
        BusinessReferenceDataPublishOperationState currentState,
        BusinessReferenceDataPublishCheckpoint currentCheckpoint,
        BusinessReferenceDataPublishOperationState nextState,
        BusinessReferenceDataPublishCheckpoint nextCheckpoint)
    {
        if (currentState is BusinessReferenceDataPublishOperationState.COMPLETED or BusinessReferenceDataPublishOperationState.FAILED_TERMINAL)
        {
            return false;
        }

        if (currentState == BusinessReferenceDataPublishOperationState.PENDING)
        {
            return currentCheckpoint == BusinessReferenceDataPublishCheckpoint.INITIALIZED
                   && nextCheckpoint == BusinessReferenceDataPublishCheckpoint.INITIALIZED
                   && nextState is BusinessReferenceDataPublishOperationState.RUNNING
                       or BusinessReferenceDataPublishOperationState.FAILED_TERMINAL;
        }

        if (currentState == BusinessReferenceDataPublishOperationState.RECOVERY_REQUIRED)
        {
            return nextState == BusinessReferenceDataPublishOperationState.RUNNING
                   && nextCheckpoint == currentCheckpoint;
        }

        if (currentState != BusinessReferenceDataPublishOperationState.RUNNING)
        {
            return false;
        }

        if (nextState == BusinessReferenceDataPublishOperationState.RECOVERY_REQUIRED)
        {
            return nextCheckpoint == currentCheckpoint;
        }

        if (nextState == BusinessReferenceDataPublishOperationState.FAILED_TERMINAL)
        {
            return currentCheckpoint < BusinessReferenceDataPublishCheckpoint.POINTER_PROMOTED
                   && nextCheckpoint == currentCheckpoint;
        }

        if (nextState == BusinessReferenceDataPublishOperationState.COMPLETED)
        {
            return currentCheckpoint == BusinessReferenceDataPublishCheckpoint.COMPLETION_VERIFIED
                   && nextCheckpoint == BusinessReferenceDataPublishCheckpoint.COMPLETION_VERIFIED;
        }

        return nextState == BusinessReferenceDataPublishOperationState.RUNNING
               && (int)nextCheckpoint == (int)currentCheckpoint + 1;
    }

    public static bool IsSameReplayTarget(
        BusinessReferenceDataPublishOperation existing,
        BusinessReferenceDataPublishOperation replay)
    {
        return existing.TenantId == replay.TenantId
               && string.Equals(existing.IdempotencyKey, replay.IdempotencyKey, StringComparison.Ordinal)
               && existing.BusinessReferenceDataSetId == replay.BusinessReferenceDataSetId
               && existing.BusinessReferenceDataVersionId == replay.BusinessReferenceDataVersionId
               && existing.ExpectedPublishedVersionId == replay.ExpectedPublishedVersionId
               && existing.ExpectedSetVersion == replay.ExpectedSetVersion
               && string.Equals(existing.CatalogVersion, replay.CatalogVersion, StringComparison.Ordinal)
               && string.Equals(existing.CatalogFingerprint, replay.CatalogFingerprint, StringComparison.Ordinal)
               && string.Equals(
                   existing.ExpectedTargetVersionToken,
                   replay.ExpectedTargetVersionToken,
                   StringComparison.Ordinal);
    }

    public static bool IsVerifiedPublication(
        BusinessReferenceDataPublishOperation operation,
        Guid? publishedVersionId,
        long currentSetVersion,
        string currentTargetVersionToken,
        bool targetVersionHasOperationPublishEvidence)
    {
        return !operation.IsDeleted
               && operation.OperationState == BusinessReferenceDataPublishOperationState.COMPLETED
               && operation.PublishCheckpoint == BusinessReferenceDataPublishCheckpoint.COMPLETION_VERIFIED
               && operation.CompletedAt.HasValue
               && HasVerifiedPostMutationContext(
                   operation,
                   publishedVersionId,
                   currentSetVersion,
                   currentTargetVersionToken,
                   targetVersionHasOperationPublishEvidence);
    }

    public static bool HasVerifiedPostMutationContext(
        BusinessReferenceDataPublishOperation operation,
        Guid? publishedVersionId,
        long currentSetVersion,
        string currentTargetVersionToken,
        bool targetVersionHasOperationPublishEvidence)
    {
        return !operation.IsDeleted
               && operation.PreMutationContextVerifiedAt.HasValue
               && publishedVersionId == operation.BusinessReferenceDataVersionId
               && operation.ExpectedSetVersion < long.MaxValue
               && currentSetVersion == operation.ExpectedSetVersion + 1
               && !string.IsNullOrWhiteSpace(currentTargetVersionToken)
               && !string.Equals(currentTargetVersionToken, operation.ExpectedTargetVersionToken, StringComparison.Ordinal)
               && targetVersionHasOperationPublishEvidence;
    }
}

[BsonIgnoreExtraElements]
public sealed class BusinessReferenceDataImportPreview : TenantScopedEntity
{
    [BsonRepresentation(BsonType.String)]
    public Guid PreviewId { get; set; } = Guid.NewGuid();

    [BsonRepresentation(BsonType.String)]
    public Guid TargetDraftVersionId { get; set; }

    public required string SetCode { get; set; }
    public required string Format { get; set; }
    public required string FileName { get; set; }
    public required string ParserKey { get; set; }
    public string? LastCorrelationId { get; set; }
    public int RowCount { get; set; }
    public int ValidRowCount { get; set; }
    public int InvalidRowCount { get; set; }
    public int BlockingErrorCount { get; set; }
    public List<BusinessReferenceDataImportPreviewRow> Rows { get; set; } = [];
    public DateTimeOffset? ExpiresAt { get; set; }
    public bool HasBlockingErrors => BlockingErrorCount > 0;
    public bool IsCommitted => CommittedAt.HasValue;
    public DateTimeOffset? CommittedAt { get; set; }
    public string? CommitIdempotencyKey { get; set; }
    public int CommitInsertedCount { get; set; }
    public int CommitUpdatedCount { get; set; }
    public int CommitDeprecatedCount { get; set; }
    public int CommitNoOpCount { get; set; }
}

[BsonIgnoreExtraElements]
public sealed class BusinessReferenceDataImportPreviewRow
{
    public int RowNumber { get; set; }
    public string? ValueCode { get; set; }
    public string? DisplayName { get; set; }
    public string? Description { get; set; }
    public string? ParentValueCode { get; set; }
    public string? ReplacementValueCode { get; set; }
    public bool IsDeprecated { get; set; }
    public int SortOrder { get; set; }
    public Dictionary<string, string>? Attributes { get; set; }
    public BusinessReferenceDataImportOperation Operation { get; set; } = BusinessReferenceDataImportOperation.NoOp;
    public List<BusinessReferenceDataImportPreviewIssue> Issues { get; set; } = [];
    public bool IsValid => Issues.Count == 0;
    public int BlockingIssueCount => Issues.Count(x => x.IsBlocking);
}

[BsonIgnoreExtraElements]
public sealed class BusinessReferenceDataImportPreviewIssue
{
    public required string RuleCode { get; set; }
    public required string Message { get; set; }
    public bool IsBlocking { get; set; }
}
