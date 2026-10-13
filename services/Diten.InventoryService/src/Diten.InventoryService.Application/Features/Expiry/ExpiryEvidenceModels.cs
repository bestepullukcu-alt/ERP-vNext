namespace Diten.InventoryService.Application.Features.Expiry;

// WP-0176-F5 — FEFO-BUNDLE 1.2.0 Kanıt Paketi wire shapes (previewEvidence, listEvidenceExports, createEvidenceExport) and the document the
// two file renderers draw. Null members are omitted on the wire.

public sealed record ExpiryEvidencePeriodDto(string? Preset, string From, string To);

public sealed record ExpiryEvidenceScopeDto(IReadOnlyList<string>? WarehouseIds, IReadOnlyList<string>? Types);

/// <summary>§ previewEvidence — the four sections: summary KPIs, the exceptions of the period, policy changes in the period, FEFO bypasses.</summary>
public sealed record ExpiryEvidencePreviewDto(
    ExpiryEvidencePeriodDto Period,
    ExpiryDashboardKpisDto Summary,
    IReadOnlyList<ExpiryExceptionRefDto> Exceptions,
    IReadOnlyList<ExpiryPolicyHistoryEntryDto> PolicyChanges,
    IReadOnlyList<ExpiryExceptionRefDto> FefoBypasses);

public sealed record ExpiryEvidenceExportDto(
    string ExportId,
    ExpiryEvidencePeriodDto Period,
    ExpiryEvidenceScopeDto Scope,
    string Format,
    string FileName,
    string Sha256,
    int RowCount,
    ExpiryUserRefDto ExportedBy,
    string ExportedAtUtc);

public sealed record ExpiryEvidenceExportPageDto(IReadOnlyList<ExpiryEvidenceExportDto> Items, long Total, int Page, int PageSize);

/// <summary>getEvidenceExportFile — the stored bytes of one export (the controller answers them as the file).</summary>
public sealed record ExpiryEvidenceFileDto(byte[] Content, string ContentType, string FileName, string Sha256);

/// <summary>One case as the CSV / PDF prints it (identifiers only: the service holds no product names — evidence open point O-F5-2).</summary>
public sealed record ExpiryEvidenceCaseRow(
    string Number,
    string Type,
    string ItemId,
    string SkuLevel,
    string LotNumber,
    string? Warehouse,
    string OpenedAtUtc,
    string Status,
    string? Resolution,
    string? ResolutionReason,
    string? SignatureId,
    string? FirstEligibleLot,
    string? BypassReason);

/// <summary>Everything one evidence file shows; the renderers add no data of their own.</summary>
public sealed record ExpiryEvidenceDocument(
    string LegalEntityId,
    string Preset,
    DateOnly From,
    DateOnly To,
    IReadOnlyList<string> WarehouseIds,
    IReadOnlyList<string> Types,
    DateTimeOffset GeneratedAtUtc,
    string Language,
    ExpiryDashboardKpisDto Summary,
    IReadOnlyList<ExpiryEvidenceCaseRow> Exceptions,
    IReadOnlyList<ExpiryPolicyHistoryEntryDto> PolicyChanges,
    IReadOnlyList<ExpiryEvidenceCaseRow> Bypasses);
