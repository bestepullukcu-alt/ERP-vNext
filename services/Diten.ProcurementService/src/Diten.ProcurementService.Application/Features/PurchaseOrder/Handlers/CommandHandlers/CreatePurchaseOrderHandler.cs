using Diten.ProcurementService.Application.Common;
using Diten.ProcurementService.Application.Features.PurchaseOrder.Commands;
using Diten.ProcurementService.Application.Features.PurchaseOrder.Validators;
using Diten.ProcurementService.Domain.Repositories;
using Diten.Shared.Core;
using MediatR;
using PurchaseOrderEntity = Diten.ProcurementService.Domain.Entities.PurchaseOrder;
using PoLineEntity = Diten.ProcurementService.Domain.Entities.PoLine;
using PoStatusEnum = Diten.ProcurementService.Domain.Entities.PoStatus;
using RequisitionStatusEnum = Diten.ProcurementService.Domain.Entities.RequisitionStatus;

namespace Diten.ProcurementService.Application.Features.PurchaseOrder.Handlers.CommandHandlers;

/// <summary>
/// createPurchaseOrder (Draft). Idempotent (Idempotency-Key). supplierId MOD-0140'ta (fail-closed → 404
/// UNKNOWN_REFERENCE), line.itemId MOD-0290'da (progressive seam, fail-closed → 404). requisitionId verilirse
/// mevcut + Approved olmalı (aksi 422 VALIDATION_FAILED). İş kuralı (≥1 satır, Decimal/float) 422. lineAmount ve
/// totalAmount SERVER-COMPUTED (Decimal; kullanıcı girmez). Stok TUTMAZ; kimlik CONSUME edilir (uydurma yok).
/// </summary>
public sealed class CreatePurchaseOrderHandler : IRequestHandler<CreatePurchaseOrderCommand, Response<PurchaseOrderDto>>
{
    private readonly IPurchaseOrderRepository _repository;
    private readonly ISupplierRepository _supplierRepository;
    private readonly IRequisitionRepository _requisitionRepository;
    private readonly IProductReferenceValidator _productValidator;

    public CreatePurchaseOrderHandler(
        IPurchaseOrderRepository repository,
        ISupplierRepository supplierRepository,
        IRequisitionRepository requisitionRepository,
        IProductReferenceValidator productValidator)
    {
        _repository = repository;
        _supplierRepository = supplierRepository;
        _requisitionRepository = requisitionRepository;
        _productValidator = productValidator;
    }

    public async Task<Response<PurchaseOrderDto>> Handle(CreatePurchaseOrderCommand request, CancellationToken cancellationToken)
    {
        // ── Idempotent create (MOD-0141 §8): aynı Idempotency-Key ile replay → mevcut kaydı döner, yeni yaratmaz ──
        if (!string.IsNullOrWhiteSpace(request.IdempotencyKey))
        {
            var replay = await _repository.GetByIdempotencyKeyAsync(request.IdempotencyKey!, cancellationToken);
            if (replay is not null)
            {
                return Response<PurchaseOrderDto>.Success(PurchaseOrderMapping.ToDto(replay), 201);
            }
        }

        var lines = request.Lines ?? new List<PoLineInput>();

        // ── İş kuralı (contract Unprocessable 422): supplier/currency + ≥1 satır + Decimal (float YASAK) ──
        if (string.IsNullOrWhiteSpace(request.SupplierId) || string.IsNullOrWhiteSpace(request.Currency))
        {
            return Response<PurchaseOrderDto>.Fail("VALIDATION_FAILED", 422);
        }
        if (lines.Count == 0)
        {
            return Response<PurchaseOrderDto>.Fail("VALIDATION_FAILED", 422);
        }
        if (lines.Any(l => !PurchaseOrderValidationRules.IsPositiveDecimal(l.Quantity)
                           || !PurchaseOrderValidationRules.IsNonNegativeDecimal(l.UnitPrice)
                           || string.IsNullOrWhiteSpace(l.ItemId)
                           || string.IsNullOrWhiteSpace(l.UomId)))
        {
            return Response<PurchaseOrderDto>.Fail("VALIDATION_FAILED", 422);
        }

        // ── SUPPLIER (0140) consume — fail-closed: bilinmeyen supplier → 404 UNKNOWN_REFERENCE ──
        var supplierId = request.SupplierId.Trim();
        var knownSuppliers = await _supplierRepository.GetBySupplierIdsAsync(new List<string> { supplierId }, cancellationToken);
        if (knownSuppliers.All(s => !string.Equals(s.SupplierId, supplierId, StringComparison.Ordinal)))
        {
            return Response<PurchaseOrderDto>.Fail("UNKNOWN_REFERENCE", 404);
        }

        // ── PRODUCT-MASTER (0290) consume — fail-closed: bilinmeyen itemId → 404 UNKNOWN_REFERENCE ──
        var itemIds = lines.Select(l => l.ItemId).Distinct(StringComparer.Ordinal).ToList();
        var unknownItems = await _productValidator.GetUnknownItemIdsAsync(itemIds, cancellationToken);
        if (unknownItems.Count > 0)
        {
            return Response<PurchaseOrderDto>.Fail("UNKNOWN_REFERENCE", 404);
        }

        // ── requisitionId verilirse: mevcut (tenant+LE) + Approved olmalı (aksi 422 VALIDATION_FAILED) ──
        var requisitionId = string.IsNullOrWhiteSpace(request.RequisitionId) ? null : request.RequisitionId!.Trim();
        if (requisitionId is not null)
        {
            var requisition = await _requisitionRepository.GetByRequisitionIdAsync(requisitionId, cancellationToken);
            if (requisition is null || requisition.Status != RequisitionStatusEnum.Approved)
            {
                return Response<PurchaseOrderDto>.Fail("VALIDATION_FAILED", 422);
            }
        }

        // ── Satır tutarları + toplam SERVER-COMPUTED (Decimal; float YASAK) ──
        var poLines = lines.Select(l =>
        {
            var quantity = l.Quantity.Trim();
            var unitPrice = l.UnitPrice.Trim();
            return new PoLineEntity
            {
                PoLineId = GeneratePoLineId(),
                ItemId = l.ItemId,
                SkuId = string.IsNullOrWhiteSpace(l.SkuId) ? null : l.SkuId,
                Quantity = quantity,
                UomId = l.UomId,
                UnitPrice = unitPrice,
                LineAmount = PurchaseOrderValidationRules.ComputeLineAmount(quantity, unitPrice)
            };
        }).ToList();

        var entity = new PurchaseOrderEntity
        {
            PoId = GeneratePoId(),
            SupplierId = supplierId,
            RequisitionId = requisitionId,
            Status = PoStatusEnum.Draft,
            Currency = request.Currency.Trim(),
            Lines = poLines,
            TotalAmount = PurchaseOrderValidationRules.ComputeTotal(poLines.Select(l => l.LineAmount)),
            SourceSystem = string.IsNullOrWhiteSpace(request.SourceSystem) ? null : request.SourceSystem!.Trim(),
            ExternalRef = string.IsNullOrWhiteSpace(request.ExternalRef) ? null : request.ExternalRef!.Trim(),
            IdempotencyKey = string.IsNullOrWhiteSpace(request.IdempotencyKey) ? null : request.IdempotencyKey
        };

        var created = await _repository.CreateAsync(entity, cancellationToken);
        return Response<PurchaseOrderDto>.Success(PurchaseOrderMapping.ToDto(created), 201);
    }

    private static string GeneratePoId()
        => "PO-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();

    private static string GeneratePoLineId()
        => "POL-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
}
