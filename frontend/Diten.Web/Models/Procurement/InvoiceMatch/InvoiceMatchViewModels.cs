using System.ComponentModel.DataAnnotations;

namespace Diten.Web.Models.Procurement.InvoiceMatch;

// MOD-0143 Invoice Capture & 3-Way Match — frontend view models.
// Fields bind ONLY the MATCH owned contract (invoice-match.openapi.yaml):
// InvoiceUpsert / Invoice / InvoiceLine / MatchOutcome / MatchException. SupplierId (MOD-0140),
// PoId (MOD-0141), Lines[].ItemId (MOD-0290) are CONSUMED, never created. Money is Decimal string (float YASAK).
// The contract exposes NO invoice update endpoint (capture + match + resolve only) → Edit is a parity-only shell.
public sealed class InvoiceMatchEditViewModel
{
    // Server-assigned public code (Invoice.invoiceId); empty on create.
    public string? InvoiceId { get; set; }

    // Consumed supplier (MOD-0140), required.
    [Required]
    public string SupplierId { get; set; } = string.Empty;

    // Consumed PO (MOD-0141), required.
    [Required]
    public string PoId { get; set; } = string.Empty;

    // Supplier invoice number (Supplier+InvoiceNumber unique/tenant+LE), required.
    [Required]
    public string InvoiceNumber { get; set; } = string.Empty;

    // Invoice currency — must match PO currency (server-enforced; mismatch → 422 CurrencyMismatch), required.
    [Required]
    public string Currency { get; set; } = string.Empty;

    // InvoiceStatus (read-only surface; server-driven). Captured on create.
    public string? Status { get; set; } = "Captured";

    // Server-computed Σ lineAmount (display-only; Decimal string).
    public string? TotalAmount { get; set; }

    // InvoiceLine[] — invoice lines. quantity/unitPrice/lineAmount are Decimal strings (float YASAK).
    public List<InvoiceLineInput> Lines { get; set; } = [new InvoiceLineInput()];

    // DEC-INV-19 external feed (nullable; capture-only — not returned by the read DTO, mirrors GRN).
    public string? SourceSystem { get; set; }
    public string? ExternalRef { get; set; }

    // ── Details-only match/exception surface (populated by the controller from real endpoints) ──
    // Open MatchException for this invoice (from GET /exceptions), enabling the Resolve action on Details.
    public string? ExceptionId { get; set; }
    public string? ExceptionReason { get; set; }
}

// InvoiceLine request/display row (contract InvoiceLine + Invoice.lines[]).
// Create binds poLineId/itemId/quantity/unitPrice/lineAmount; lineAmount is optional (server-computed if omitted).
public sealed class InvoiceLineInput
{
    // Upstream MOD-0141 PO line reference — nullable; not created.
    public string? PoLineId { get; set; }

    // MOD-0290 product reference (uuid) — consumed opaque (fail-closed).
    public string? ItemId { get; set; }

    // Decimal string (float YASAK).
    public string? Quantity { get; set; }
    public string? UnitPrice { get; set; }
    public string? LineAmount { get; set; }
}

// ── Deserialization of the MATCH contract Invoice (inside the house Response<T>.Data) ──
// InvoiceStatus is serialized numerically by the service (no JsonStringEnumConverter observed;
// Captured=0, Matched=1, MatchedWithinTolerance=2, Exception=3, Rejected=4, ClearedForPayment=5);
// bound as int? and mapped to canonical contract names in the controller.
public sealed class InvoiceResponseApiModel
{
    public string InvoiceId { get; set; } = string.Empty;
    public string? SupplierId { get; set; }
    public string? PoId { get; set; }
    public string? InvoiceNumber { get; set; }
    public int? Status { get; set; }
    public string? Currency { get; set; }
    public List<InvoiceLineResultApiModel> Lines { get; set; } = [];
    public string? TotalAmount { get; set; }
    public string? ContractVersion { get; set; }
}

public sealed class InvoiceLineResultApiModel
{
    public string? PoLineId { get; set; }
    public string? ItemId { get; set; }
    public string? Quantity { get; set; }
    public string? UnitPrice { get; set; }
    public string? LineAmount { get; set; }
}

// listMatchExceptions result — backend MatchExceptionListResultDto {items, nextCursor, contractVersion}.
// MatchException.reasonCode/status serialized numerically (ExceptionReason/ExceptionStatus enum ordinals).
public sealed class MatchExceptionApiModel
{
    public string ExceptionId { get; set; } = string.Empty;
    public string InvoiceId { get; set; } = string.Empty;
    public string? PoId { get; set; }
    public int? ReasonCode { get; set; }
    public int? Status { get; set; }
    public string? ContractVersion { get; set; }
}

public sealed class MatchExceptionListData
{
    public List<MatchExceptionApiModel> Items { get; set; } = [];
    public string? NextCursor { get; set; }
    public string? ContractVersion { get; set; }
}

// POST /api/invoice-match/invoices payload — InvoiceUpsert shape (server resolves TenantId/LegalEntityId;
// Idempotency-Key is a header).
public sealed class InvoiceRequestPayload
{
    public string SupplierId { get; set; } = string.Empty;
    public string PoId { get; set; } = string.Empty;
    public string InvoiceNumber { get; set; } = string.Empty;
    public string Currency { get; set; } = string.Empty;
    public List<InvoiceLinePayload> Lines { get; set; } = [];
    public string? SourceSystem { get; set; }
    public string? ExternalRef { get; set; }
}

public sealed class InvoiceLinePayload
{
    public string? PoLineId { get; set; }
    public string ItemId { get; set; } = string.Empty;
    public string Quantity { get; set; } = string.Empty;
    public string UnitPrice { get; set; } = string.Empty;
    public string? LineAmount { get; set; }
}

// House response envelope (Response<T>): { data, isSuccessful, statusCode, errors }.
public sealed class GatewayResponse<T>
{
    public T? Data { get; set; }
    public bool IsSuccessful { get; set; }
    public int StatusCode { get; set; }
    public List<string> Errors { get; set; } = [];
}
