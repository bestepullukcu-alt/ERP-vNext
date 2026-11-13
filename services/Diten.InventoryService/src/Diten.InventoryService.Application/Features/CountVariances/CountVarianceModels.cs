using System.Text.Json;
using Diten.InventoryService.Application.Features.CycleCounting;
using Diten.InventoryService.Application.Features.CycleCounts;

namespace Diten.InventoryService.Application.Features.CountVariances;

// WMS-COUNT-BUNDLE v1 draft §3.6–§3.9 (DL-3) — the decision / correction / settings DTOs and the Variance Trace rows (Golden Slim: one models
// file). Empty optional members are not written (draft §1 rule 4).

/// <summary>Draft §3.6 <c>CycleCountDecision</c> (pack §4.2): the label is the key <c>CycleCount.Decision.{kind}</c> / <c>.Source.{source}</c>.</summary>
public sealed record CycleCountDecisionDto
{
    public required string Kind { get; init; }
    public required string Source { get; init; }
    public string? By { get; init; }
    public required string At { get; init; }
    public string? Reason { get; init; }
}

/// <summary>
/// Draft §3.7 <c>CycleCountAdjustment</c> (pack §4.2, §8.6). <c>quantity</c> follows the blind rule of the line's quantities (absent where the
/// expected quantity is); <c>refusalDetails</c> is the ledger's <c>details</c> object as it answered.
/// </summary>
public sealed record CycleCountAdjustmentDto
{
    public required string MovementType { get; init; }
    public string? Quantity { get; init; }
    public required string IdempotencyKey { get; init; }
    public required string Status { get; init; }
    public string? TransactionId { get; init; }
    public string? TransactionNumber { get; init; }
    public string? RefusalCode { get; init; }
    public JsonElement? RefusalDetails { get; init; }
    public int? RefusalStatus { get; init; }
    public string? IntegrityReason { get; init; }
    public string? HolderTransactionId { get; init; }
    public required string TransactionDate { get; init; }
    public required string PostingDate { get; init; }
    public required int Attempts { get; init; }
    public required string FirstAttemptAt { get; init; }
    public required string LastAttemptAt { get; init; }
}

/// <summary>Draft §3.7 <c>CycleCountIntegrityResolution</c> (pack §4.2, F-12).</summary>
public sealed record CycleCountIntegrityResolutionDto
{
    public required string Kind { get; init; }
    public required string By { get; init; }
    public required string At { get; init; }
    public required string Reason { get; init; }
    public string? HolderTransactionId { get; init; }
}

/// <summary>Draft §3.8 — a manual <c>COUNT_*</c> ledger movement (pack §2.4): read only, no action; <c>sourceMismatch</c> = it declares MOD-0181 but matches no line.</summary>
public sealed record CountManualMovementDto
{
    public required string TransactionId { get; init; }
    public required string TransactionNumber { get; init; }
    public required string MovementType { get; init; }
    public string? WarehouseId { get; init; }
    public string? LocationId { get; init; }
    public required CycleCountItemRefDto Item { get; init; }
    public string? LotId { get; init; }
    public string? StockStatus { get; init; }
    public required string Quantity { get; init; }
    public required string UomId { get; init; }
    public required string SourceModule { get; init; }
    public required string SourceType { get; init; }
    public string? SourceDocumentId { get; init; }
    public string? SourceLineId { get; init; }
    public required string CreatedBy { get; init; }
    public required string CreatedAt { get; init; }
    public required string TransactionDate { get; init; }
    public required string PostingDate { get; init; }
    public required bool SourceMismatch { get; init; }
}

/// <summary>
/// Draft §3.8 <c>CountVariance</c> — one Variance Trace row: <c>source = Count</c> (a 0181 line with its count number and the ledger movement
/// that is its correction by the §2.4 rule) or <c>source = Manual</c> (a manual <c>COUNT_*</c> movement).
/// </summary>
public sealed record CountVarianceDto
{
    public required string Source { get; init; }
    public string? CountNumber { get; init; }
    public CycleCountLineDto? Line { get; init; }
    public string? LedgerTransactionId { get; init; }
    public string? LedgerTransactionNumber { get; init; }
    public CountManualMovementDto? Movement { get; init; }
    public string ContractVersion { get; init; } = CycleCountContract.ContractVersion;
}

public sealed record CountVariancePageDto(IReadOnlyList<CountVarianceDto> Items, long Total, int Page, int PageSize,
    string ContractVersion = CycleCountContract.ContractVersion);

/// <summary>Draft §3.9 <c>CycleCountSettings</c> (pack §4.4): <c>isDefault</c> = no record yet (version 0).</summary>
public sealed record CycleCountSettingsDto
{
    public required string ToleranceAbsolute { get; init; }
    public required string TolerancePercent { get; init; }
    public required int MaxRecounts { get; init; }
    public required int MaxLinesPerCount { get; init; }
    public required bool IsDefault { get; init; }
    public string? UpdatedBy { get; init; }
    public string? UpdatedAt { get; init; }
    public required int Version { get; init; }
    public string ContractVersion { get; init; } = CycleCountContract.ContractVersion;
}

/// <summary><c>requestCountRecount</c> body without the key (pack §8.5): the selected lines and the request fields — never the scope or <c>blind</c>.</summary>
public sealed record CycleCountRecountInput(IReadOnlyList<Guid> LineIds, string? Reason, Guid? AssignedTo, string? PlannedDate, string? Note);

/// <summary><c>updateCycleCountSettings</c> body without the key (pack §4.4) — the decimals as sent (strings), the integers as numbers.</summary>
public sealed record CycleCountSettingsInput(string? ToleranceAbsolute, string? TolerancePercent, int MaxRecounts, int MaxLinesPerCount);
