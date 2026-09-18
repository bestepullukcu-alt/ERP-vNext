using Diten.Shared.Core;
using MediatR;

namespace Diten.ProcurementService.Application.Features.InvoiceMatch.Commands;

/// <summary>
/// captureInvoice (contract POST /api/invoice-match/invoices). Tenant/LE server-resolved — payload'da YOK.
/// Idempotency-Key ile idempotent (replay → aynı fatura, yeni kayıt YOK). supplierId MOD-0140 + poId MOD-0141 +
/// line.itemId MOD-0290 CONSUME edilir (fail-closed → 404 UNKNOWN_REFERENCE). Currency PO currency ile eşleşmeli
/// (uyumsuz → 422 CURRENCY_MISMATCH). (SupplierId+InvoiceNumber) duplicate → 409 DUPLICATE_INVOICE. ≥1 satır +
/// Decimal/float → 422. Ödeme YÜRÜTMEZ — yalnız fatura yakalar (status=Captured).
/// </summary>
public sealed record CaptureInvoiceCommand(
    string SupplierId,
    string PoId,
    string InvoiceNumber,
    string Currency,
    List<InvoiceLineInput>? Lines,
    string? SourceSystem,
    string? ExternalRef,
    string? IdempotencyKey) : IRequest<Response<InvoiceDto>>;
