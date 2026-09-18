using Diten.ProcurementService.Application.Common;
using Diten.ProcurementService.Application.Features.Grn.Commands;
using Diten.ProcurementService.Domain.Repositories;
using Diten.Shared.Core;
using MediatR;
using GrnStatusEnum = Diten.ProcurementService.Domain.Entities.GrnStatus;

namespace Diten.ProcurementService.Application.Features.Grn.Handlers.CommandHandlers;

/// <summary>
/// reverseGrn (ASSUMPTION-GRN-02). GRN kaydı EDIT EDİLMEZ; her satır için INVENTORY REVERSAL hareketi post edilir
/// (append-only, DEC-INV-07) ve Status → Reversed olur. Yalnız Posted reverse edilebilir (aksi 409 INVALID_STATE).
/// Cross-tenant/LE → 404 (repository null). Idempotency-Key ile idempotent: aynı key ile replay → ikinci REVERSAL
/// hareketi YOK. Balance düzeltmesi INVENTORY'de (MOD-0173); GRN yalnız reverse transaction referansını günceller.
/// </summary>
public sealed class ReverseGrnHandler : IRequestHandler<ReverseGrnCommand, Response<GrnResponseDto>>
{
    private readonly IGrnRepository _repository;
    private readonly IInventoryPostingClient _inventoryClient;

    public ReverseGrnHandler(IGrnRepository repository, IInventoryPostingClient inventoryClient)
    {
        _repository = repository;
        _inventoryClient = inventoryClient;
    }

    public async Task<Response<GrnResponseDto>> Handle(ReverseGrnCommand request, CancellationToken cancellationToken)
    {
        var entity = await _repository.GetByGrnIdAsync(request.GrnId, cancellationToken);
        if (entity is null)
        {
            return Response<GrnResponseDto>.Fail("UNKNOWN_GRN", 404);
        }

        // Idempotent replay: aynı reverse key + zaten Reversed → mevcut kaydı döner (ikinci REVERSAL yok).
        if (!string.IsNullOrWhiteSpace(request.IdempotencyKey)
            && entity.Status == GrnStatusEnum.Reversed
            && string.Equals(entity.ReverseIdempotencyKey, request.IdempotencyKey, StringComparison.Ordinal))
        {
            return Response<GrnResponseDto>.Success(GrnMapping.ToDto(entity));
        }

        // Yalnız Posted reverse edilebilir (MOD-0142 §16).
        if (entity.Status != GrnStatusEnum.Posted)
        {
            return Response<GrnResponseDto>.Fail("INVALID_STATE", 409);
        }

        var reverseKey = string.IsNullOrWhiteSpace(request.IdempotencyKey)
            ? entity.GrnId + ":reverse"
            : request.IdempotencyKey!;

        for (var i = 0; i < entity.Lines.Count; i++)
        {
            var line = entity.Lines[i];
            // INVENTORY REVERSAL — düzeltme yalnız yeni hareketle (GRN edit edilmez).
            var movement = new InventoryMovementRequest(
                IdempotencyKey: $"{reverseKey}#{i}",
                MovementType: InventoryMovementTypes.Reversal,
                ItemId: line.ItemId,
                SkuId: line.SkuId,
                SkuLevel: line.SkuLevel.ToString(),
                WarehouseId: entity.WarehouseId,
                LocationId: entity.LocationId,
                Quantity: line.Quantity,
                UomId: line.UomId,
                LotNumber: line.LotNumber,
                SerialIds: line.SerialIds,
                ToStockStatus: line.ToStockStatus.ToString(),
                SourceModule: GrnMovementSource.SourceModule,
                SourceType: GrnMovementSource.SourceType,
                SourceDocumentId: entity.GrnId,
                SourceLineId: line.PoLineId);

            await _inventoryClient.PostMovementAsync(movement, cancellationToken);
        }

        entity.Status = GrnStatusEnum.Reversed;
        entity.ReverseIdempotencyKey = reverseKey;

        var ok = await _repository.UpdateAsync(entity, entity.Version, cancellationToken);
        if (!ok)
        {
            return Response<GrnResponseDto>.Fail("CONFLICT", 409);
        }

        return Response<GrnResponseDto>.Success(GrnMapping.ToDto(entity));
    }
}
