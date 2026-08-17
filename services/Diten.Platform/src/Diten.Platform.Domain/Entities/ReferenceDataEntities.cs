using Diten.Platform.Common.Persistence;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Diten.Platform.Domain.Entities;

[BsonIgnoreExtraElements]
public sealed class ReferenceDataSet : TenantScopedEntity
{
    [BsonRepresentation(BsonType.String)]
    public Guid ReferenceDataSetId { get; set; } = Guid.NewGuid();

    public required string SetCode { get; set; }
    public required string Name { get; set; }
    public required string ScopeType { get; set; }
    public string? Description { get; set; }
    public ReferenceDataSetStatus Status { get; set; } = ReferenceDataSetStatus.Draft;

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
public sealed class ReferenceDataVersion : TenantScopedEntity
{
    [BsonRepresentation(BsonType.String)]
    public Guid ReferenceDataVersionId { get; set; } = Guid.NewGuid();

    [BsonRepresentation(BsonType.String)]
    public Guid ReferenceDataSetId { get; set; }

    public int VersionNumber { get; set; }
    public ReferenceDataVersionStatus Status { get; set; } = ReferenceDataVersionStatus.Draft;
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
    public GovernanceState GovernanceState { get; set; } = GovernanceState.Draft;
    public ApprovalState ApprovalState { get; set; } = ApprovalState.NotStarted;
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
    public List<ReferenceDataValue> Values { get; set; } = [];
    public List<ReferenceDataAttributeDefinition> AttributeDefinitions { get; set; } = [];
    public List<ReferenceDataMapping> Mappings { get; set; } = [];

    [BsonRepresentation(BsonType.String)]
    public Guid? SupersededByVersionId { get; set; }
}

[BsonIgnoreExtraElements]
public sealed class ReferenceDataValidationResult : TenantScopedEntity
{
    [BsonRepresentation(BsonType.String)]
    public Guid ValidationResultId { get; set; } = Guid.NewGuid();

    [BsonRepresentation(BsonType.String)]
    public Guid ReferenceDataVersionId { get; set; }

    public required string RuleId { get; set; }
    public ValidationSeverity Severity { get; set; } = ValidationSeverity.Info;
    public bool IsBlocking { get; set; }
    public required string Message { get; set; }
    public bool IsStubbed { get; set; }
    public string? StubReason { get; set; }
    public DateTimeOffset ExecutedAt { get; set; } = DateTimeOffset.UtcNow;
    public string? CorrelationId { get; set; }
}

public enum ReferenceDataSetStatus
{
    Draft,
    Active,
    Retired
}

public enum ReferenceDataVersionStatus
{
    Draft,
    Published,
    Deprecated,
    Retired
}

public enum ValidationSeverity
{
    Error,
    Warning,
    Info
}

public enum GovernanceState
{
    Draft,
    Submitted,
    InReview,
    Approved,
    Rejected
}

public enum ApprovalState
{
    NotStarted,
    Pending,
    Approved,
    Rejected
}

public enum ReferenceDataImportOperation
{
    Insert,
    Update,
    Deprecate,
    NoOp
}

[BsonIgnoreExtraElements]
public sealed class ReferenceDataValue
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
public sealed class ReferenceDataAttributeDefinition
{
    public required string AttributeCode { get; set; }
    public required string DisplayName { get; set; }
    public string DataType { get; set; } = "string";
    public bool IsRequired { get; set; }
}

[BsonIgnoreExtraElements]
public sealed class ReferenceDataMapping
{
    public required string MappingKey { get; set; }
    public required string SourceValueCode { get; set; }
    public required string TargetCode { get; set; }
    public string? TargetLabel { get; set; }
}

[BsonIgnoreExtraElements]
public sealed class ReferenceDataIntegrationEvent : TenantScopedEntity
{
    [BsonRepresentation(BsonType.String)]
    public Guid IntegrationEventId { get; set; } = Guid.NewGuid();

    [BsonRepresentation(BsonType.String)]
    public Guid ReferenceDataVersionId { get; set; }

    public required string EventName { get; set; }
    public int EventVersion { get; set; } = 1;
    public required string IdempotencyKey { get; set; }
    public required string PayloadJson { get; set; }
    public DateTimeOffset EmittedAt { get; set; } = DateTimeOffset.UtcNow;
}

[BsonIgnoreExtraElements]
public sealed class ReferenceDataUsageRegistration : TenantScopedEntity
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
public sealed class ReferenceDataImportPreview : TenantScopedEntity
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
    public List<ReferenceDataImportPreviewRow> Rows { get; set; } = [];
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
public sealed class ReferenceDataImportPreviewRow
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
    public ReferenceDataImportOperation Operation { get; set; } = ReferenceDataImportOperation.NoOp;
    public List<ReferenceDataImportPreviewIssue> Issues { get; set; } = [];
    public bool IsValid => Issues.Count == 0;
    public int BlockingIssueCount => Issues.Count(x => x.IsBlocking);
}

[BsonIgnoreExtraElements]
public sealed class ReferenceDataImportPreviewIssue
{
    public required string RuleCode { get; set; }
    public required string Message { get; set; }
    public bool IsBlocking { get; set; }
}
