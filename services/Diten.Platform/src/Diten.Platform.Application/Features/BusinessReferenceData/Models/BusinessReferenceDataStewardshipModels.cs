namespace Diten.Platform.Application.Features.BusinessReferenceData.Models;

public sealed record BusinessReferenceDataSetListItemModel(
    Guid SetId,
    string SetCode,
    string Name,
    string ScopeType,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    long RowVersion,
    Guid? ActiveDraftVersionId,
    Guid? PublishedVersionId,
    string GovernanceStatus,
    string ApprovalStatus);

public sealed record BusinessReferenceDataScopeTypeModel(
    string Value,
    string Name);

public sealed record BusinessReferenceDataUsageFormOptionModel(
    string Value,
    string Label);

public sealed record BusinessReferenceDataUsageFormOptionsModel(
    IReadOnlyList<BusinessReferenceDataUsageFormOptionModel> ConsumerModules,
    IReadOnlyList<BusinessReferenceDataUsageFormOptionModel> ScopeTypes,
    IReadOnlyList<string> ScopeKeys,
    string? SetScopeType = null,
    IReadOnlyDictionary<string, IReadOnlyList<string>>? ScopeKeysByScopeType = null);

public sealed record BusinessReferenceDataSetListModel(
    IReadOnlyList<BusinessReferenceDataSetListItemModel> Items,
    int Page,
    int PageSize,
    long TotalCount,
    int TotalPages);

public sealed record BusinessReferenceDataSetDetailModel(
    Guid SetId,
    string SetCode,
    string Name,
    string ScopeType,
    string? Description,
    string Status,
    Guid? ActiveDraftVersionId,
    Guid? PublishedVersionId,
    long RowVersion,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

public sealed record BusinessReferenceDataVersionHistoryItemModel(
    Guid VersionId,
    Guid SetId,
    int VersionNumber,
    string Status,
    string BusinessReferenceDataGovernanceState,
    string BusinessReferenceDataApprovalState,
    bool IsEditable,
    bool IsImmutable,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    DateTimeOffset? SubmittedAt,
    DateTimeOffset? DecisionAt,
    DateTimeOffset? PublishedAt,
    string? PublishedBy,
    string? LastEvidenceRef,
    Guid? SourceVersionId,
    Guid? SupersededByVersionId);

public sealed record BusinessReferenceDataVersionHistoryModel(
    Guid SetId,
    IReadOnlyList<BusinessReferenceDataVersionHistoryItemModel> Items);

public sealed record BusinessReferenceDataVersionDetailModel(
    Guid VersionId,
    Guid SetId,
    int VersionNumber,
    string Status,
    string ConcurrencyToken,
    bool IsImmutable,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    DateTimeOffset? PublishedAt,
    string? PublishedBy,
    Guid? SourceVersionId,
    Guid? TargetDraftVersionId,
    string? CopyActor,
    DateTimeOffset? CopiedAt,
    string BusinessReferenceDataGovernanceState,
    string BusinessReferenceDataApprovalState,
    bool IsEditable,
    DateTimeOffset? SubmittedAt,
    string? SubmittedBy,
    DateTimeOffset? DecisionAt,
    string? DecisionBy,
    string? RejectionReason,
    string? WorkflowTemplateCode,
    Guid? WorkflowInstanceId,
    bool IsOverrideAction,
    string? OverrideReason,
    string? LastEvidenceRef,
    string? LastPublishIdempotencyKey,
    string? LastPublishMode,
    Guid? SupersededByVersionId,
    string? EvidenceLinkId = null,
    Guid? EvidenceEvaluationId = null,
    string? EvidenceDocumentVersionId = null,
    string? EvidenceRequirementCode = null,
    string? EvidenceDecisionCode = null,
    string? EvidenceReasonCode = null);

public sealed record BusinessReferenceDataValidationResultModel(
    string RuleId,
    string Severity,
    bool IsBlocking,
    string Message,
    bool IsStubbed,
    string? StubReason);

public sealed record BusinessReferenceDataValidationRunModel(
    Guid VersionId,
    int BlockingErrorCount,
    int WarningCount,
    int InfoCount,
    bool BlocksSubmitOrPublish,
    IReadOnlyList<string> PublishBlockers,
    IReadOnlyList<BusinessReferenceDataValidationResultModel> Results);

public sealed record BusinessReferenceDataConsumerValueModel(
    string ValueCode,
    string DisplayName,
    string? Description,
    bool IsDeprecated,
    string? ReplacementValueCode,
    string? ParentValueCode,
    int SortOrder,
    DateTimeOffset? EffectiveFrom,
    DateTimeOffset? EffectiveTo,
    IReadOnlyDictionary<string, string>? Attributes);

public sealed record BusinessReferenceDataMappingModel(
    string MappingKey,
    string SourceValueCode,
    string TargetCode,
    string? TargetLabel);

public sealed record BusinessReferenceDataValuesLookupModel(
    string SetCode,
    string ScopeType,
    string? ScopeKey,
    int VersionNumber,
    Guid VersionId,
    DateTimeOffset? PublishedAt,
    IReadOnlyList<BusinessReferenceDataConsumerValueModel> Values,
    IReadOnlyList<string> AttributeCodes,
    IReadOnlyList<BusinessReferenceDataMappingModel> Mappings);

public sealed record BusinessReferenceDataPublishedValueItemModel(
    string Code,
    string Label,
    string? Description,
    bool IsActive,
    int SortOrder,
    IReadOnlyDictionary<string, string>? Attributes);

public sealed record BusinessReferenceDataPublishedValuesModel(
    string SetCode,
    int VersionNumber,
    DateTimeOffset? PublishedAt,
    IReadOnlyList<BusinessReferenceDataPublishedValueItemModel> Items);

public sealed record BusinessReferenceDataVersionValueItemModel(
    string Code,
    string Label,
    string? Description,
    bool IsActive,
    int SortOrder,
    string? ParentValueCode,
    IReadOnlyDictionary<string, string>? Attributes);

public sealed record BusinessReferenceDataVersionValuesModel(
    Guid VersionId,
    string Status,
    bool IsEditable,
    string? ConcurrencyToken,
    IReadOnlyList<BusinessReferenceDataVersionValueItemModel> Items);

public sealed record BusinessReferenceDataVersionAttributeDefinitionItemModel(
    string AttributeCode,
    string DisplayName,
    string DataType,
    bool IsRequired);

public sealed record BusinessReferenceDataVersionAttributeDefinitionsModel(
    Guid VersionId,
    string Status,
    bool IsEditable,
    string? ConcurrencyToken,
    IReadOnlyList<BusinessReferenceDataVersionAttributeDefinitionItemModel> Items);

public sealed record BusinessReferenceDataVersionMappingItemModel(
    string MappingKey,
    string SourceValueCode,
    string TargetCode,
    string? TargetLabel);

public sealed record BusinessReferenceDataVersionMappingsModel(
    Guid VersionId,
    string Status,
    bool IsEditable,
    string? ConcurrencyToken,
    IReadOnlyList<BusinessReferenceDataVersionMappingItemModel> Items);

public sealed record BusinessReferenceDataHierarchyNodeModel(
    string ValueCode,
    string DisplayName,
    bool IsDeprecated,
    IReadOnlyList<BusinessReferenceDataHierarchyNodeModel> Children);

public sealed record BusinessReferenceDataHierarchyLookupModel(
    string SetCode,
    string ScopeType,
    string? ScopeKey,
    int VersionNumber,
    Guid VersionId,
    DateTimeOffset? PublishedAt,
    IReadOnlyList<BusinessReferenceDataConsumerValueModel> Flattened,
    IReadOnlyList<BusinessReferenceDataHierarchyNodeModel> Tree,
    IReadOnlyList<string> AttributeCodes,
    IReadOnlyList<BusinessReferenceDataMappingModel> Mappings);

public sealed record BusinessReferenceDataUsageImpactSummaryModel(
    int TotalRegistrations,
    int CriticalRegistrations,
    int HighRegistrations,
    int MediumRegistrations,
    int LowRegistrations,
    DateTimeOffset? LastRegisteredAt);

public sealed record BusinessReferenceDataUsageRegistrationResultModel(
    Guid UsageRegistrationId,
    string SetCode,
    string ConsumerModule,
    string ConsumerName,
    string? ScopeType,
    string? ScopeKey,
    int? VersionPin,
    DateTimeOffset? AsOfDate,
    string ResolutionMode,
    string Criticality,
    Guid? LastResolvedVersionId,
    DateTimeOffset? LastResolvedAt,
    BusinessReferenceDataUsageImpactSummaryModel ImpactSummary);

public sealed record BusinessReferenceDataUsageRegistrationListItemModel(
    Guid UsageRegistrationId,
    string SetCode,
    string ConsumerModule,
    string ConsumerName,
    string? ConsumerEndpoint,
    string? ScopeType,
    string? ScopeKey,
    int? VersionPin,
    DateTimeOffset? AsOfDate,
    string ResolutionMode,
    string Criticality,
    Guid? LastResolvedVersionId,
    DateTimeOffset? LastResolvedAt,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

public sealed record BusinessReferenceDataUsageRegistrationListModel(
    string SetCode,
    IReadOnlyList<BusinessReferenceDataUsageRegistrationListItemModel> Items,
    BusinessReferenceDataUsageImpactSummaryModel ImpactSummary);

public sealed record BusinessReferenceDataImportPreviewIssueModel(
    string RuleCode,
    string Message,
    bool IsBlocking);

public sealed record BusinessReferenceDataImportPreviewRowModel(
    int RowNumber,
    string? ValueCode,
    string? DisplayName,
    string Operation,
    bool IsValid,
    int BlockingIssueCount,
    IReadOnlyList<BusinessReferenceDataImportPreviewIssueModel> Issues);

public sealed record BusinessReferenceDataImportErrorReportRowModel(
    int RowNumber,
    string? ValueCode,
    string Operation,
    string RuleCode,
    bool IsBlocking,
    string Message);

public sealed record BusinessReferenceDataImportErrorReportModel(
    string ContentType,
    string FileName,
    IReadOnlyList<BusinessReferenceDataImportErrorReportRowModel> Rows);

public sealed record BusinessReferenceDataImportPreviewModel(
    Guid PreviewId,
    Guid TargetDraftVersionId,
    string SetCode,
    string Format,
    string Parser,
    int RowCount,
    int ValidRowCount,
    int InvalidRowCount,
    int BlockingErrorCount,
    IReadOnlyList<BusinessReferenceDataImportPreviewRowModel> Rows,
    BusinessReferenceDataImportErrorReportModel ErrorReport);

public sealed record BusinessReferenceDataImportCommitResultModel(
    Guid PreviewId,
    Guid TargetDraftVersionId,
    string IdempotencyKey,
    int InsertedCount,
    int UpdatedCount,
    int DeprecatedCount,
    int NoOpCount,
    DateTimeOffset CommittedAt,
    bool IsIdempotentReplay);

public sealed record BusinessReferenceDataEvidenceFixtureProvisionModel(
    string FixtureCode,
    Guid SetId,
    string SetCode,
    long SetRowVersion,
    Guid VersionId,
    string VersionConcurrencyToken,
    string RequirementCode,
    string ValueCode,
    bool RequiresEvidence,
    bool RequiresApproval);

public sealed record BusinessReferenceDataEvidenceFixtureRetireModel(
    string FixtureCode,
    Guid SetId,
    string SetCode,
    string Status,
    long RowVersion);
