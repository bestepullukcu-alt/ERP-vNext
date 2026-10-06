using Diten.ProcurementService.Application.Common;
using Diten.ProcurementService.Application.Features.InvoiceMatch.Commands;
using Diten.ProcurementService.Application.Features.InvoiceMatch.Validators;
using Diten.ProcurementService.Domain.Repositories;
using Diten.Shared.Core;
using MediatR;
using InvoiceEntity = Diten.ProcurementService.Domain.Entities.Invoice;
using InvoiceLineEntity = Diten.ProcurementService.Domain.Entities.InvoiceLine;
using InvoiceStatusEnum = Diten.ProcurementService.Domain.Entities.InvoiceStatus;

namespace Diten.ProcurementService.Application.Features.InvoiceMatch.Handlers.CommandHandlers;

/// <summary>
/// captureInvoice. Idempotent (Idempotency-Key → replay mevcut faturayı döner, yeni yaratmaz). CONSUME-don't-own
/// (fail-closed): supplierId MOD-0140 (bilinmeyen → 404 UNKNOWN_REFERENCE), poId MOD-0141 (bilinmeyen → 404),
/// line.itemId MOD-0290 (progressive seam, bilinmeyen → 404). Currency PO currency ile eşleşmeli (uyumsuz → 422
/// CURRENCY_MISMATCH). (SupplierId+InvoiceNumber) duplicate → 409 DUPLICATE_INVOICE. ≥1 satır + Decimal/float → 422.
/// lineAmount/totalAmount SERVER-COMPUTED (Decimal; float YASAK). Ödeme YÜRÜTMEZ — status=Captured yakalar.
/// </summary>
public sealed class CaptureInvoiceHandler : IRequestHandler<CaptureInvoiceCommand, Response<InvoiceDto>>
{
    private readonly IInvoiceMatchRepository _repository;
    private readonly ISupplierRepository _supplierRepository;
    private readonly IPurchaseOrderRepository _purchaseOrderRepository;
    private readonly IProductReferenceValidator _productValidator;

    public CaptureInvoiceHandler(
        IInvoiceMatchRepository repository,
        ISupplierRepository supplierRepository,
        IPurchaseOrderRepository purchaseOrderRepository,
        IProductReferenceValidator productValidator)
    {
        _repository = repository;
        _supplierRepository = supplierRepository;
        _purchaseOrderRepository = purchaseOrderRepository;
        _productValidator = productValidator;
    }

    public async Task<Response<InvoiceDto>> Handle(CaptureInvoiceCommand request, CancellationToken cancellationToken)
    {
        // ── Idempotent create: aynı Idempotency-Key ile replay → mevcut fatura döner, yeni yaratmaz ──
        if (!string.IsNullOrWhiteSpace(request.IdempotencyKey))
        {
            var replay = await _repository.GetByIdempotencyKeyAsync(request.IdempotencyKey!, cancellationToken);
            if (replay is not null)
            {
                return Response<InvoiceDto>.Success(InvoiceMatchMapping.ToDto(replay), 201);
            }
        }

        var lines = request.Lines ?? new List<InvoiceLineInput>();

        // ── İş kuralı (contract Unprocessable 422): zorunlu alanlar + ≥1 satır + Decimal (float YASAK) ──
        if (string.IsNullOrWhiteSpace(request.SupplierId)
            || string.IsNullOrWhiteSpace(request.PoId)
            || string.IsNullOrWhiteSpace(request.InvoiceNumber)
            || string.IsNullOrWhiteSpace(request.Currency))
        {
            return Response<InvoiceDto>.Fail("VALIDATION_FAILED", 422);
        }
        if (lines.Count == 0)
        {
            return Response<InvoiceDto>.Fail("VALIDATION_FAILED", 422);
        }
        if (lines.Any(l => string.IsNullOrWhiteSpace(l.ItemId)
                           || !InvoiceMatchValidationRules.IsPositiveDecimal(l.Quantity)
                           || !InvoiceMatchValidationRules.IsNonNegativeDecimal(l.UnitPrice)))
        {
            return Response<InvoiceDto>.Fail("VALIDATION_FAILED", 422);
        }

        var supplierId = request.SupplierId.Trim();
        var poId = request.PoId.Trim();
        var invoiceNumber = request.InvoiceNumber.Trim();
        var currency = request.Currency.Trim();

        // ── SUPPLIER (0140) consume — fail-closed: bilinmeyen supplier → 404 UNKNOWN_REFERENCE ──
        var knownSuppliers = await _supplierRepository.GetBySupplierIdsAsync(new List<string> { supplierId }, cancellationToken);
        if (knownSuppliers.All(s => !string.Equals(s.SupplierId, supplierId, StringComparison.Ordinal)))
        {
            return Response<InvoiceDto>.Fail("UNKNOWN_REFERENCE", 404);
        }

        // ── PO (0141) consume — fail-closed: bilinmeyen PO → 404 UNKNOWN_REFERENCE ──
        var po = await _purchaseOrderRepository.GetByPoIdAsync(poId, cancellationToken);
        if (po is null)
        {
            return Response<InvoiceDto>.Fail("UNKNOWN_REFERENCE", 404);
        }

        // ── PRODUCT-MASTER (0290) consume — fail-closed: bilinmeyen itemId → 404 UNKNOWN_REFERENCE ──
        var itemIds = lines.Select(l => l.ItemId.Trim()).Distinct(StringComparer.Ordinal).ToList();
        var unknownItems = await _productValidator.GetUnknownItemIdsAsync(itemIds, cancellationToken);
        if (unknownItems.Count > 0)
        {
            return Response<InvoiceDto>.Fail("UNKNOWN_REFERENCE", 404);
        }

        // ── Currency PO ile eşleşmeli — uyumsuz → 422 CURRENCY_MISMATCH (capture engellenir) ──
        if (!string.Equals(currency, po.Currency?.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return Response<InvoiceDto>.Fail("CURRENCY_MISMATCH", 422);
        }

        // ── Duplicate (SupplierId+InvoiceNumber) → 409 DUPLICATE_INVOICE (kayıt yazılmaz) ──
        if (await _repository.ExistsByInvoiceNumberAsync(supplierId, invoiceNumber, cancellationToken))
        {
            return Response<InvoiceDto>.Fail("DUPLICATE_INVOICE", 409);
        }

        // ── Satır tutarları + toplam SERVER-COMPUTED (Decimal; float YASAK) ──
        var invoiceLines = lines.Select(l =>
        {
            var quantity = l.Quantity.Trim();
            var unitPrice = l.UnitPrice.Trim();
            var lineAmount = InvoiceMatchValidationRules.IsValidDecimal(l.LineAmount)
                ? l.LineAmount!.Trim()
                : InvoiceMatchValidationRules.ComputeLineAmount(quantity, unitPrice);
            return new InvoiceLineEntity
            {
                PoLineId = string.IsNullOrWhiteSpace(l.PoLineId) ? null : l.PoLineId!.Trim(),
                ItemId = l.ItemId.Trim(),
                Quantity = quantity,
                UnitPrice = unitPrice,
                LineAmount = lineAmount
            };
        }).ToList();

        var entity = new InvoiceEntity
        {
            InvoiceId = GenerateInvoiceId(),
            SupplierId = supplierId,
            PoId = poId,
            InvoiceNumber = invoiceNumber,
            Currency = currency,
            Status = InvoiceStatusEnum.Captured,
            Lines = invoiceLines,
            TotalAmount = InvoiceMatchValidationRules.ComputeTotal(invoiceLines.Select(l => l.LineAmount)),
            SourceSystem = string.IsNullOrWhiteSpace(request.SourceSystem) ? null : request.SourceSystem!.Trim(),
            ExternalRef = string.IsNullOrWhiteSpace(request.ExternalRef) ? null : request.ExternalRef!.Trim(),
            IdempotencyKey = string.IsNullOrWhiteSpace(request.IdempotencyKey) ? null : request.IdempotencyKey
        };

        var created = await _repository.CreateAsync(entity, cancellationToken);
        return Response<InvoiceDto>.Success(InvoiceMatchMapping.ToDto(created), 201);
    }

    private static string GenerateInvoiceId()
        => "INV-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
}
