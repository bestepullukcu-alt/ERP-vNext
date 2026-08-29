using Diten.CrmService.Application.Common.ReferenceValidation;

namespace Diten.CrmService.Application.Features.Territory.ImportExport;

/// <summary>Everything the workbook writer needs. Rows are pre-shaped as strings by the handler (schema order).</summary>
public sealed record TerritoryWorkbookRequest(
    bool IsTemplate,
    string ModelCode,
    string ModelName,
    string ModelStatus,
    IReadOnlyDictionary<string, IReadOnlyList<IReadOnlyList<string?>>> Sheets,
    IReadOnlyList<ReferenceSetSnapshot> ReferenceSets,
    string GeneratedAtUtc,
    string CorrelationId);

/// <summary>
/// One dry-run / apply row outcome. The field set is the pack §22.5 row contract:
/// Sheet · RowNumber · Severity · ErrorCode · Message · SuggestedFix · Blocking · Operation · EntityType ·
/// ResolvedKey · ChangedFields.
/// </summary>
public sealed record TerritoryImportRowResultDto(
    string Sheet,
    int RowNumber,
    string Severity,
    string? ErrorCode,
    string Message,
    string? SuggestedFix,
    bool Blocking,
    string? Operation,
    string EntityType,
    string? ResolvedKey,
    IReadOnlyList<string> ChangedFields,
    string Status);

public sealed record TerritoryImportSummaryDto(
    int TotalRows,
    int Creates,
    int Updates,
    int Ends,
    int Skips,
    int Errors,
    int Conflicts,
    int Warnings);

/// <summary>Per-sheet outcome so a partial apply is always explicit — a silent partial apply is forbidden (§22.5).</summary>
public sealed record TerritoryImportSheetOutcomeDto(
    string Sheet,
    int TotalRows,
    int BlockingRows,
    bool Applied,
    string? NotAppliedReason,
    int Created,
    int Updated,
    int Ended,
    int Skipped);

/// <summary>Dry-run preview or apply result for a whole workbook.</summary>
public sealed record TerritoryImportPreviewDto(
    string CorrelationId,
    Guid ModelId,
    string ModelCode,
    string ModelStatus,
    bool DryRun,
    bool Applied,
    bool CanApply,
    string? BlockedReason,
    string Strategy,
    bool StrictMode,
    string FileHash,
    int PreviousAppliesOfThisFile,
    Guid? ImportRunId,
    string? RunStatus,
    TerritoryImportSummaryDto Summary,
    IReadOnlyList<string> FileErrors,
    IReadOnlyList<string> FileWarnings,
    IReadOnlyList<TerritoryImportSheetOutcomeDto> Sheets,
    IReadOnlyList<TerritoryImportRowResultDto> Rows);

public sealed record TerritoryImportRunListDto(int TotalCount, IReadOnlyList<TerritoryImportRunDto> Items);

public sealed record TerritoryImportRunDto(
    Guid ImportRunId,
    Guid TerritoryModelId,
    string ModelCode,
    string FileName,
    string FileHash,
    string UploadedBy,
    DateTimeOffset UploadedAt,
    string Status,
    DateTimeOffset? AppliedAt,
    string? AppliedBy,
    string? CorrelationId,
    int TotalRows,
    int Creates,
    int Updates,
    int Ends,
    int Skips,
    int ErrorCount,
    int WarningCount,
    bool StrictMode,
    IReadOnlyList<string> SheetOutcomes,
    IReadOnlyList<TerritoryImportRunSheetCountDto> SheetCounts);

public sealed record TerritoryImportRunSheetCountDto(
    string Sheet, int Total, int Created, int Updated, int Ended, int Skipped);

/// <summary>Import run lifecycle values persisted on <c>TerritoryImportRun.Status</c>.</summary>
public static class TerritoryImportRunStatuses
{
    public const string Applied = "applied";
    public const string PartiallyApplied = "partially-applied";
    public const string Failed = "failed";
    public const string Blocked = "blocked";
}

public static class TerritoryImportSeverities
{
    public const string Info = "info";
    public const string Warning = "warning";
    public const string Error = "error";
}
