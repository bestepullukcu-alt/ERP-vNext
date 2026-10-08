namespace Diten.PlanningService.Domain.Features.DemandPlanning;

public enum ImportIssueSeverity { Warning, Blocking }
public enum ImportBatchValidationState { Validated, ValidatedWithWarnings, Blocked }
public enum ImportBatchReviewState { Pending, Approved, Rejected }

public sealed class ImportValidationIssue
{
    public string Code { get; set; } = string.Empty;
    public ImportIssueSeverity Severity { get; set; }
    public string Message { get; set; } = string.Empty;
}

public sealed class DemandHistoryImportRow
{
    public int RowNumber { get; set; }
    public List<string> RawFields { get; set; } = [];
    public string SourceRecordKey { get; set; } = string.Empty;
    public Guid? SkuId { get; set; }
    public string SkuLevel { get; set; } = string.Empty;
    public string WarehouseId { get; set; } = string.Empty;
    public DateOnly? OccurredOn { get; set; }
    public decimal? OriginalQuantity { get; set; }
    public string OriginalUomId { get; set; } = string.Empty;
    public string RecordKind { get; set; } = string.Empty;
    public string? RelatedSourceRecordKey { get; set; }
    public string? BaseUomId { get; set; }
    public decimal? ConversionFactor { get; set; }
    public string? ConversionContractId { get; set; }
    public string? ConversionContractVersion { get; set; }
    public decimal? BaseQuantity { get; set; }
    public List<ImportValidationIssue> Issues { get; set; } = [];
    public bool IsQuarantined => Issues.Any(x => x.Severity == ImportIssueSeverity.Blocking);
}

public sealed class DemandHistoryQuarantineCase
{
    public int RowNumber { get; set; }
    public string State { get; set; } = "Open";
    public List<string> ReasonCodes { get; set; } = [];
}

public sealed class DemandHistoryImportAuditAction
{
    public string Action { get; set; } = string.Empty;
    public Guid ActorId { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
    public string Detail { get; set; } = string.Empty;
    public string? RequestKey { get; set; }
    public string? Reason { get; set; }
}

// This stores unapproved file rows and validation evidence, never accepted demand history.
public sealed class DemandHistoryImportBatch : EntityBase
{
    public Guid LegalEntityId { get; set; }
    public Guid UploadedByActorId { get; set; }
    public string SourceChannel { get; set; } = "File";
    public string SourceSystem { get; set; } = string.Empty;
    public string SourceFileName { get; set; } = string.Empty;
    public string SourceFileSha256 { get; set; } = string.Empty;
    public DateOnly ScopeFrom { get; set; }
    public DateOnly ScopeThrough { get; set; }
    public List<string> WarehouseScope { get; set; } = [];
    public List<string> FieldScope { get; set; } = [];
    public ImportBatchValidationState ValidationState { get; set; }
    public bool ApprovalBlocked { get; set; }
    public DateTimeOffset ValidatedAt { get; set; }
    public string IdempotencyKey { get; set; } = string.Empty;
    public string RequestFingerprint { get; set; } = string.Empty;
    public List<ImportValidationIssue> FileIssues { get; set; } = [];
    public List<DemandHistoryImportRow> Rows { get; set; } = [];
    public List<DemandHistoryQuarantineCase> QuarantineCases { get; set; } = [];
    public List<DemandHistoryImportAuditAction> AuditTrail { get; set; } = [];
    public ImportBatchReviewState ReviewState { get; set; } = ImportBatchReviewState.Pending;
    public Guid? ReviewedByActorId { get; set; }
    public DateTimeOffset? ReviewedAt { get; set; }
    public string? ReviewReason { get; set; }
    public string? ReviewRequestKey { get; set; }
    public string? ReviewRequestFingerprint { get; set; }

    // Review accepts a batch decision only; it does not accept rows as demand history.
    public bool IsEligibleForIndependentReview() =>
        ReviewState == ImportBatchReviewState.Pending && !ApprovalBlocked &&
        ValidationState != ImportBatchValidationState.Blocked &&
        !FileIssues.Any(x => x.Severity == ImportIssueSeverity.Blocking) &&
        !Rows.Any(x => x.Issues.Any(y => y.Severity == ImportIssueSeverity.Blocking)) &&
        !QuarantineCases.Any(x => x.State == "Open");
}
