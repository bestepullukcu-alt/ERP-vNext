using Diten.PlanningService.Domain.Features.DemandPlanning;

namespace Diten.PlanningService.Application.Features.DemandPlanning.HistoryImports;

public sealed class HistoryImportFileLimitException : Exception { }

public sealed record ParsedHistoryRow(int RowNumber, IReadOnlyList<string> RawFields, string? ParseError);
public sealed record ParsedHistoryFile(
    IReadOnlyList<string> HeaderFields,
    IReadOnlyList<ParsedHistoryRow> Rows,
    IReadOnlyList<ImportValidationIssue> FileIssues);

public interface IHistoryImportFileParser
{
    ParsedHistoryFile Parse(ReadOnlyMemory<byte> fileBytes);
}

// Scope must be verified server side. Null means the authority is unavailable, never allowed.
public interface IHistoryImportScopeAuthority : ILegalEntityAssignmentAuthority
{
    Task<bool?> IsAuthorizedAsync(Guid tenantId, Guid actorId, Guid legalEntityId,
        DateOnly from, DateOnly through, IReadOnlyList<string> warehouses,
        CancellationToken cancellationToken);
}

public enum HistoryReferenceState { Verified, Invalid, Unavailable }
public sealed record HistoryReferenceResult(
    HistoryReferenceState State,
    string? BaseUomId,
    decimal? ConversionFactor,
    string? ConversionContractId,
    string? ConversionContractVersion);

// Frozen contract/mock seam for SKU, Warehouse eligibility and dated UoM conversion.
public interface IHistoryRowReferenceChecker
{
    Task<HistoryReferenceResult> CheckAsync(Guid tenantId, Guid legalEntityId,
        Guid skuId, string skuLevel, string warehouseId, DateOnly occurredOn,
        string originalUomId, CancellationToken cancellationToken);
}

public enum HistoryImportInsertOutcome { Created, Existing, Conflict }
public sealed record HistoryImportInsertResult(
    HistoryImportInsertOutcome Outcome, DemandHistoryImportBatch? Batch);

public interface IHistoryImportBatchStore
{
    Task<HistoryImportInsertResult> InsertOrGetAsync(
        DemandHistoryImportBatch batch, CancellationToken cancellationToken);
    Task<DemandHistoryImportBatch?> GetOwnAsync(Guid tenantId, Guid actorId,
        Guid batchId, CancellationToken cancellationToken);
    Task<DemandHistoryImportBatch?> GetForReviewAsync(Guid tenantId,
        Guid batchId, CancellationToken cancellationToken);
    Task<DemandHistoryImportBatch?> TryDecideAsync(DemandHistoryImportBatch expected,
        Guid reviewerId, ImportBatchReviewState decision, string reason,
        string requestKey, string fingerprint, DateTimeOffset decidedAt,
        CancellationToken cancellationToken);
}

public sealed record HistoryImportReviewFailure(
    Guid TenantId, Guid LegalEntityId, Guid BatchId, Guid ActorId,
    string RequestKey, string Fingerprint, string Outcome, string Reason,
    DateTimeOffset OccurredAt);

public interface IHistoryImportReviewAuditStore
{
    Task AppendOnceAsync(HistoryImportReviewFailure failure, CancellationToken cancellationToken);
    Task<IReadOnlyList<HistoryImportReviewFailure>> ListAsync(Guid tenantId,
        Guid batchId, CancellationToken cancellationToken);
}

public sealed record HistoryImportRowResult(
    int RowNumber, string SourceRecordKey, bool IsQuarantined,
    IReadOnlyList<ImportValidationIssue> Issues);
public sealed record HistoryImportBatchResult(
    Guid BatchId, Guid TenantId, Guid LegalEntityId, Guid UploadedByActorId,
    string SourceChannel, string SourceSystem, string SourceFileName,
    string SourceFileSha256, DateOnly ScopeFrom, DateOnly ScopeThrough,
    IReadOnlyList<string> WarehouseScope, IReadOnlyList<string> FieldScope,
    ImportBatchValidationState ValidationState, bool ApprovalBlocked,
    int RowCount, int QuarantineCount,
    IReadOnlyList<ImportValidationIssue> FileIssues,
    IReadOnlyList<HistoryImportRowResult> Rows,
    ImportBatchReviewState ReviewState, Guid? ReviewedByActorId,
    DateTimeOffset? ReviewedAt, string? ReviewReason,
    IReadOnlyList<DemandHistoryImportAuditAction> AuditTrail);

public sealed record HistoryImportReviewView(
    HistoryImportBatchResult Batch, IReadOnlyList<HistoryImportReviewFailure> FailedAttempts);

public static class HistoryImportResultMapper
{
    public static HistoryImportBatchResult Map(DemandHistoryImportBatch batch) => new(
        batch.Id, batch.TenantId, batch.LegalEntityId, batch.UploadedByActorId,
        batch.SourceChannel, batch.SourceSystem, batch.SourceFileName,
        batch.SourceFileSha256, batch.ScopeFrom, batch.ScopeThrough,
        batch.WarehouseScope.AsReadOnly(), batch.FieldScope.AsReadOnly(),
        batch.ValidationState, batch.ApprovalBlocked, batch.Rows.Count,
        batch.QuarantineCases.Count, batch.FileIssues.AsReadOnly(),
        batch.Rows.Select(r => new HistoryImportRowResult(
            r.RowNumber, r.SourceRecordKey, r.IsQuarantined,
            r.Issues.AsReadOnly())).ToList().AsReadOnly(),
        batch.ReviewState, batch.ReviewedByActorId, batch.ReviewedAt,
        batch.ReviewReason, batch.AuditTrail.AsReadOnly());
}


