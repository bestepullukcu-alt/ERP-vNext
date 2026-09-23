using Diten.ProcurementService.Domain.Entities;

namespace Diten.ProcurementService.Application.Features.InvoiceMatch;

// ── Contract version sabiti (MATCH owned contract; invoice-match.openapi.yaml) ──
public static class InvoiceMatchContract
{
    public const string Version = "v1";
}

/// <summary>MatchOutcome.variances[].field kanonik enum değerleri (contract lowercase: quantity|price|amount).</summary>
public static class VarianceFields
{
    public const string Quantity = "quantity";
    public const string Price = "price";
    public const string Amount = "amount";
}

// ══ READ DTOs (invoice-match.openapi.yaml şekilleriyle uyumlu) ═══════════════════

/// <summary>Invoice.lines[] öğesi — contract InvoiceLine {poLineId?, itemId, quantity, unitPrice, lineAmount}.</summary>
public sealed record InvoiceLineDto(
    string? PoLineId,
    string ItemId,
    string Quantity,
    string UnitPrice,
    string LineAmount);

/// <summary>Invoice read DTO — contract Invoice {invoiceId, supplierId, poId, invoiceNumber, status, currency,
/// lines[], totalAmount, contractVersion}. (Id/Version house-style zarf alanı; concurrency için.)</summary>
public sealed record InvoiceDto(
    Guid Id,
    string InvoiceId,
    string SupplierId,
    string PoId,
    string InvoiceNumber,
    InvoiceStatus Status,
    string Currency,
    IReadOnlyList<InvoiceLineDto> Lines,
    string TotalAmount,
    int Version,
    string ContractVersion);

/// <summary>MatchOutcome.variances[] öğesi — contract {poLineId?, field, expected, actual, withinTolerance}.</summary>
public sealed record VarianceDto(
    string? PoLineId,
    string Field,
    string Expected,
    string Actual,
    bool WithinTolerance);

/// <summary>MatchOutcome read DTO — contract {invoiceId, poId, grnIds[], result, toleranceProfileId?, variances[],
/// exceptionId?, contractVersion}.</summary>
public sealed record MatchOutcomeDto(
    string InvoiceId,
    string PoId,
    IReadOnlyList<string> GrnIds,
    InvoiceMatchResult Result,
    string? ToleranceProfileId,
    IReadOnlyList<VarianceDto> Variances,
    string? ExceptionId,
    string ContractVersion);

/// <summary>MatchException read DTO — contract MatchException {exceptionId, invoiceId, poId, reasonCode, status,
/// contractVersion}.</summary>
public sealed record MatchExceptionDto(
    string ExceptionId,
    string InvoiceId,
    string? PoId,
    MatchExceptionReason ReasonCode,
    MatchExceptionStatus Status,
    string ContractVersion);

/// <summary>Exception kuyruğu sayfası — contract listMatchExceptions {items, nextCursor, contractVersion}.</summary>
public sealed record MatchExceptionListResultDto(
    IReadOnlyList<MatchExceptionDto> Items,
    string? NextCursor,
    string ContractVersion);

/// <summary>Fatura register sayfası — contract listInvoices {items, nextCursor, contractVersion}. Kardeş modüllerin
/// (PO/Requisition/GRN/Contract) list-result şekliyle aynı; InvoiceId sıralı cursor sayfalama.</summary>
public sealed record InvoiceListResultDto(
    IReadOnlyList<InvoiceDto> Items,
    string? NextCursor,
    string ContractVersion);

// ══ REQUEST / INPUT records (controller body binding — contract InvoiceUpsert vb.) ══

/// <summary>contract InvoiceLine request satırı (lineAmount opsiyonel; verilmezse server-hesaplı).</summary>
public sealed record InvoiceLineInput(
    string? PoLineId,
    string ItemId,
    string Quantity,
    string UnitPrice,
    string? LineAmount);

/// <summary>contract InvoiceUpsert body. TenantId/LegalEntityId server-resolved (payload'da YOK); Idempotency-Key header.</summary>
public sealed record InvoiceUpsertBody(
    string SupplierId,
    string PoId,
    string InvoiceNumber,
    string Currency,
    List<InvoiceLineInput>? Lines,
    string? SourceSystem,
    string? ExternalRef);

/// <summary>runThreeWayMatch body — toleranceProfileId opsiyonel (policy-driven; verilmezse default profil seam'de çözülür).</summary>
public sealed record RunThreeWayMatchBody(
    string? ToleranceProfileId);

/// <summary>resolveMatchException body — decision (approve/reject/tolerance-override) + opsiyonel note.</summary>
public sealed record ResolveMatchExceptionBody(
    string Decision,
    string? Note);
