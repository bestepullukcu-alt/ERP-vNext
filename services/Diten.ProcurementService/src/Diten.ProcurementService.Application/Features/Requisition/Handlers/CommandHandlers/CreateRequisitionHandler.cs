using Diten.ProcurementService.Application.Common;
using Diten.ProcurementService.Application.Features.Requisition.Commands;
using Diten.ProcurementService.Application.Features.Requisition.Validators;
using Diten.ProcurementService.Domain.Repositories;
using Diten.Shared.Core;
using MediatR;
using RequisitionEntity = Diten.ProcurementService.Domain.Entities.Requisition;
using RequisitionLineEntity = Diten.ProcurementService.Domain.Entities.RequisitionLine;
using RequisitionStatusEnum = Diten.ProcurementService.Domain.Entities.RequisitionStatus;

namespace Diten.ProcurementService.Application.Features.Requisition.Handlers.CommandHandlers;

/// <summary>
/// createRequisition (Draft). Idempotent (Idempotency-Key). line.itemId MOD-0290'da (progressive seam, fail-closed)
/// doğrulanır → bilinmeyen referans 404 UNKNOWN_REFERENCE. İş kuralı (≥1 satır, Decimal/float) 422 VALIDATION_FAILED
/// (contract Unprocessable). Stok TUTMAZ; ürün kimliği CONSUME edilir (uydurma yok).
/// </summary>
public sealed class CreateRequisitionHandler : IRequestHandler<CreateRequisitionCommand, Response<RequisitionDto>>
{
    private readonly IRequisitionRepository _repository;
    private readonly IProductReferenceValidator _productValidator;

    public CreateRequisitionHandler(
        IRequisitionRepository repository,
        IProductReferenceValidator productValidator)
    {
        _repository = repository;
        _productValidator = productValidator;
    }

    public async Task<Response<RequisitionDto>> Handle(CreateRequisitionCommand request, CancellationToken cancellationToken)
    {
        // ── Idempotent create (MOD-0141 §8): aynı Idempotency-Key ile replay → mevcut kaydı döner, yeni yaratmaz ──
        if (!string.IsNullOrWhiteSpace(request.IdempotencyKey))
        {
            var replay = await _repository.GetByIdempotencyKeyAsync(request.IdempotencyKey!, cancellationToken);
            if (replay is not null)
            {
                return Response<RequisitionDto>.Success(RequisitionMapping.ToDto(replay), 201);
            }
        }

        var lines = request.Lines ?? new List<RequisitionLineInput>();

        // ── İş kuralı (contract Unprocessable 422): ≥1 satır + Decimal string (float YASAK) + itemId/uomId zorunlu ──
        if (lines.Count == 0)
        {
            return Response<RequisitionDto>.Fail("VALIDATION_FAILED", 422);
        }
        if (lines.Any(l => !RequisitionValidationRules.IsPositiveDecimal(l.Quantity)
                           || string.IsNullOrWhiteSpace(l.ItemId)
                           || string.IsNullOrWhiteSpace(l.UomId)))
        {
            return Response<RequisitionDto>.Fail("VALIDATION_FAILED", 422);
        }

        // ── PRODUCT-MASTER (0290) consume — fail-closed: bilinmeyen itemId → 404 UNKNOWN_REFERENCE ──
        var itemIds = lines.Select(l => l.ItemId).Distinct(StringComparer.Ordinal).ToList();
        var unknownItems = await _productValidator.GetUnknownItemIdsAsync(itemIds, cancellationToken);
        if (unknownItems.Count > 0)
        {
            return Response<RequisitionDto>.Fail("UNKNOWN_REFERENCE", 404);
        }

        var entity = new RequisitionEntity
        {
            RequisitionId = GenerateRequisitionId(),
            Status = RequisitionStatusEnum.Draft,
            Justification = string.IsNullOrWhiteSpace(request.Justification) ? null : request.Justification!.Trim(),
            Lines = lines.Select(l => new RequisitionLineEntity
            {
                ItemId = l.ItemId,
                SkuId = string.IsNullOrWhiteSpace(l.SkuId) ? null : l.SkuId,
                Quantity = l.Quantity.Trim(),
                UomId = l.UomId,
                NeedBy = string.IsNullOrWhiteSpace(l.NeedBy) ? null : l.NeedBy!.Trim()
            }).ToList(),
            IdempotencyKey = string.IsNullOrWhiteSpace(request.IdempotencyKey) ? null : request.IdempotencyKey
        };

        var created = await _repository.CreateAsync(entity, cancellationToken);
        return Response<RequisitionDto>.Success(RequisitionMapping.ToDto(created), 201);
    }

    private static string GenerateRequisitionId()
        => "REQ-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
}
