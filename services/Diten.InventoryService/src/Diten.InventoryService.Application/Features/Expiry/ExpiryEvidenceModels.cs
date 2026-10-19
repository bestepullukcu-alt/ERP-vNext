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

/// <summary>
/// One case as the CSV / PDF prints it. WP-MVP1-FIX-LIVE2 (FU-0176-EV-NAMES, additive): the names beside the ids, read at generation time —
/// <see cref="ItemText"/> (PRODUCT-MASTER code · name), <see cref="SkuCode"/>, <see cref="WarehouseName"/> (code · name), <see cref="SignerName"/>
/// (MOD-0022 <c>signerNameAtSign</c>) with <see cref="SignerUserId"/>; an unreadable one is the id + "(okunamadı)" / "(ad okunamadı)".
/// </summary>
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
    string? BypassReason,
    string? ItemText = null,
    string? SkuCode = null,
    string? WarehouseName = null,
    string? SignerName = null,
    string? SignerUserId = null);

/// <summary>
/// Everything one evidence file shows; the renderers add no data of their own. WP-MVP1-FIX-LIVE2 (additive): <see cref="CompanyText"/> (MDM code ·
/// name), <see cref="WarehouseTexts"/> (the scope's warehouses, code · name), <see cref="PolicyChangedByNames"/> (one per policy change, the
/// central audit queue's name for that change's correlation id and user), <see cref="RequestedLanguage"/> (the selected language when the PDF is
/// drawn in English — zh / ar). No Expiry entity gained a field: every name is read back from where it was recorded.
/// </summary>
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
    IReadOnlyList<ExpiryEvidenceCaseRow> Bypasses,
    string? CompanyText = null,
    IReadOnlyList<string>? WarehouseTexts = null,
    IReadOnlyList<string>? PolicyChangedByNames = null,
    string? RequestedLanguage = null)
{
    public string Company => string.IsNullOrEmpty(CompanyText) ? LegalEntityId : CompanyText;

    public bool EnglishFallback => RequestedLanguage is { } r && r != Language;
}
