namespace Diten.InventoryService.Application.Features.CountEvidenceExports;

// MOD-0181 DL-5 — WMS-COUNT-BUNDLE draft §3.11 / §4.4 (binding in DL-5): the Evidence Pack wire shapes and the document both renderers draw.
// Null members are omitted on the wire (rule 4).

/// <summary>Draft §3.11 <c>CountEvidenceExport</c> — one export of the log (insert-only).</summary>
public sealed record CountEvidenceExportDto
{
    public required string ExportId { get; init; }
    public required string CountId { get; init; }
    public required string CountNumber { get; init; }
    public required string Format { get; init; }
    public required string Language { get; init; }
    public required string RequestedLanguage { get; init; }
    public required string RequestedBy { get; init; }
    public required string RequestedAt { get; init; }
    public required string FileName { get; init; }
    public required string Sha256 { get; init; }
    public required long SizeBytes { get; init; }
    public required string Status { get; init; }
    public string ContractVersion { get; init; } = CycleCounting.CycleCountContract.ContractVersion;
}

public sealed record CountEvidenceExportPageDto(IReadOnlyList<CountEvidenceExportDto> Items, long Total, int Page, int PageSize,
    string ContractVersion = CycleCounting.CycleCountContract.ContractVersion);

/// <summary><c>getCountEvidenceExportFile</c> — the STORED bytes and what the record says of them (the controller answers them as the file).</summary>
public sealed record CountEvidenceFileDto(byte[] Content, string ContentType, string FileName, string Sha256);

/// <summary>The count head as the file prints it (codes stay codes; the renderers word them).</summary>
public sealed record CountEvidenceHeader(
    string Number,
    string Warehouse,
    string CountType,
    bool Blind,
    string Status,
    string PlannedDate,
    DateTimeOffset? SnapshotAt,
    DateTimeOffset? ClosedAt,
    IReadOnlyList<string> ScopeBins,
    DateTimeOffset? ManualFrom,
    DateTimeOffset ManualTo);

/// <summary>One count line: the quantities (pack §8.4), its state, the decision (who / when / why) and the ledger correction.</summary>
public sealed record CountEvidenceLineRow(
    string Bin,
    string Sku,
    string? Lot,
    string Origin,
    string Expected,
    string? MovementsSinceSnapshot,
    string? ExpectedAtCount,
    string? Counted,
    string? Variance,
    string Status,
    string? DecisionKind,
    string? DecisionSource,
    string? DecidedBy,
    DateTimeOffset? DecidedAt,
    string? Reason,
    string? AdjustmentStatus,
    string? TransactionNumber);

/// <summary>One manual <c>COUNT_*</c> movement on the scope's bins between SnapshotAt and the close (pack §2.4) — not a line's correction.</summary>
public sealed record CountEvidenceManualRow(
    DateTimeOffset At,
    string Bin,
    string Sku,
    string? Lot,
    string MovementType,
    string Quantity,
    string TransactionNumber,
    string? SourceModule,
    string? SourceType,
    string CreatedBy,
    bool SourceMismatch);

/// <summary>One timeline row (the count's <c>wms_count_events</c>).</summary>
public sealed record CountEvidenceEventRow(int Seq, DateTimeOffset At, string Kind, string? Actor);

/// <summary>
/// Everything one evidence file shows — the PDF and the CSV draw the SAME document (Q-18 = A); the renderers add no data of their own.
/// <see cref="Language"/> is the language the file is drawn in; <see cref="RequestedLanguage"/> differs only when a PDF fell back to English.
/// </summary>
public sealed record CountEvidenceDocument(
    string Language,
    string RequestedLanguage,
    DateTimeOffset GeneratedAt,
    string Company,
    CountEvidenceHeader Header,
    IReadOnlyList<CountEvidenceLineRow> Lines,
    IReadOnlyList<CountEvidenceManualRow> Manual,
    IReadOnlyList<CountEvidenceEventRow> Timeline)
{
    public bool EnglishFallback => !string.Equals(RequestedLanguage, Language, StringComparison.Ordinal);
}
