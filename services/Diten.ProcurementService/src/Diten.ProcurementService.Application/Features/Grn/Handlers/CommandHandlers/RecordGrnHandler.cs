using Diten.ProcurementService.Application.Common;
using Diten.ProcurementService.Application.Features.Grn.Commands;
using Diten.ProcurementService.Application.Features.Grn.Validators;
using Diten.ProcurementService.Domain.Repositories;
using Diten.Shared.Core;
using MediatR;
using GoodsReceiptEntity = Diten.ProcurementService.Domain.Entities.GoodsReceipt;
using GrnLineEntity = Diten.ProcurementService.Domain.Entities.GrnLine;
using GrnStatusEnum = Diten.ProcurementService.Domain.Entities.GrnStatus;
using GoodsReceivedEventEntity = Diten.ProcurementService.Domain.Entities.GoodsReceivedEventRecord;

namespace Diten.ProcurementService.Application.Features.Grn.Handlers.CommandHandlers;

/// <summary>
/// recordGrn — THE CRITICAL SLICE (INVENTORY posting). Idempotent (Idempotency-Key). line.itemId MOD-0290'da
/// (fail-closed → 422 UNKNOWN_ITEM). poId verilirse mevcut olmalı (aksi 422). HER satır frozen INVENTORY-BUNDLE
/// <c>POST /movements</c> (GOODS_RECEIPT_PO) ile <see cref="IInventoryPostingClient"/> üzerinden post edilir ve dönen
/// <c>inventoryTransactionId</c> GRN satırında SALT REFERANS olarak saklanır.
///
/// ⚠ NO SHADOW STOCK (MOD-0142 §2/§8): GRN yalnız mal-kabul DOKÜMANINI (+ dönen transaction/lot referansını) tutar;
/// hiçbir stok balance/ledger AÇMAZ. Envanter gerçeği tek SoR MOD-0173'te. line.Quantity mal-kabul dokümanı
/// miktarıdır (ne teslim alındı), on-hand balance DEĞİL. Idempotency: replay → mevcut GRN döner, INVENTORY seam
/// TEKRAR çağrılmaz (mükerrer hareket yok).
/// </summary>
public sealed class RecordGrnHandler : IRequestHandler<RecordGrnCommand, Response<GrnResponseDto>>
{
    private readonly IGrnRepository _repository;
    private readonly IPurchaseOrderRepository _purchaseOrderRepository;
    private readonly IProductReferenceValidator _productValidator;
    private readonly IInventoryPostingClient _inventoryClient;

    public RecordGrnHandler(
        IGrnRepository repository,
        IPurchaseOrderRepository purchaseOrderRepository,
        IProductReferenceValidator productValidator,
        IInventoryPostingClient inventoryClient)
    {
        _repository = repository;
        _purchaseOrderRepository = purchaseOrderRepository;
        _productValidator = productValidator;
        _inventoryClient = inventoryClient;
    }

    public async Task<Response<GrnResponseDto>> Handle(RecordGrnCommand request, CancellationToken cancellationToken)
    {
        // ── Idempotent create (MOD-0142 §8): aynı Idempotency-Key ile replay → mevcut GRN döner. INVENTORY seam
        //    TEKRAR çağrılmaz → mükerrer hareket YOK, ikinci balance artışı YOK. ──
        if (!string.IsNullOrWhiteSpace(request.IdempotencyKey))
        {
            var replay = await _repository.GetByIdempotencyKeyAsync(request.IdempotencyKey!, cancellationToken);
            if (replay is not null)
            {
                return Response<GrnResponseDto>.Success(GrnMapping.ToDto(replay), 201);
            }
        }

        var lines = request.Lines ?? new List<GrnLineInput>();

        // ── İş kuralı (contract GrnUnprocessable 422): warehouse + ≥1 satır + satır alanları + Decimal/enum ──
        if (string.IsNullOrWhiteSpace(request.WarehouseId))
        {
            return Response<GrnResponseDto>.Fail("VALIDATION_FAILED", 422);
        }
        if (lines.Count == 0)
        {
            return Response<GrnResponseDto>.Fail("VALIDATION_FAILED", 422);
        }
        if (lines.Any(l => string.IsNullOrWhiteSpace(l.ItemId)
                           || string.IsNullOrWhiteSpace(l.SkuId)
                           || string.IsNullOrWhiteSpace(l.UomId)
                           || !GrnValidationRules.IsPositiveDecimal(l.Quantity)
                           || !GrnValidationRules.TryParseSkuLevel(l.SkuLevel, out _)
                           || !GrnValidationRules.TryParseStockStatus(l.ToStockStatus, out _)))
        {
            return Response<GrnResponseDto>.Fail("VALIDATION_FAILED", 422);
        }

        // ── PRODUCT-MASTER (0290) consume — fail-closed: bilinmeyen itemId → 422 UNKNOWN_ITEM (silent-pass YOK) ──
        var itemIds = lines.Select(l => l.ItemId).Distinct(StringComparer.Ordinal).ToList();
        var unknownItems = await _productValidator.GetUnknownItemIdsAsync(itemIds, cancellationToken);
        if (unknownItems.Count > 0)
        {
            return Response<GrnResponseDto>.Fail("UNKNOWN_ITEM", 422);
        }

        // ── poId verilirse: upstream MOD-0141 PO mevcut olmalı (tenant+LE filtreli); aksi 422 UNKNOWN_PO ──
        var poId = string.IsNullOrWhiteSpace(request.PoId) ? null : request.PoId!.Trim();
        if (poId is not null)
        {
            var po = await _purchaseOrderRepository.GetByPoIdAsync(poId, cancellationToken);
            if (po is null)
            {
                return Response<GrnResponseDto>.Fail("UNKNOWN_PO", 422);
            }
        }

        // GrnId post ÖNCESİ üretilir — INVENTORY MovementRequest.sourceDocumentId olarak gönderilir (§19).
        var grnId = GenerateGrnId();
        var warehouseId = request.WarehouseId.Trim();
        var locationId = string.IsNullOrWhiteSpace(request.LocationId) ? null : request.LocationId!.Trim();
        var receivedAt = DateTimeOffset.UtcNow;
        var idempotencyKey = string.IsNullOrWhiteSpace(request.IdempotencyKey) ? null : request.IdempotencyKey;

        var grnLines = new List<GrnLineEntity>(lines.Count);
        var events = new List<GoodsReceivedEventEntity>(lines.Count);

        for (var i = 0; i < lines.Count; i++)
        {
            var input = lines[i];
            GrnValidationRules.TryParseSkuLevel(input.SkuLevel, out var skuLevel);
            GrnValidationRules.TryParseStockStatus(input.ToStockStatus, out var toStockStatus);
            var quantity = input.Quantity.Trim();
            var poLineId = string.IsNullOrWhiteSpace(input.PoLineId) ? null : input.PoLineId!.Trim();

            // ── INVENTORY posting (stok değiştiren TEK yol) — GOODS_RECEIPT_PO. Satır bazlı deterministik
            //    idempotencyKey → MOD-0173 replay-safe. Dönen transactionId GRN'de SALT REFERANS olarak saklanır. ──
            var movement = new InventoryMovementRequest(
                IdempotencyKey: LineMovementKey(idempotencyKey ?? grnId, i),
                MovementType: InventoryMovementTypes.GoodsReceiptPo,
                ItemId: input.ItemId.Trim(),
                SkuId: input.SkuId.Trim(),
                SkuLevel: skuLevel.ToString(),
                WarehouseId: warehouseId,
                LocationId: locationId,
                Quantity: quantity,
                UomId: input.UomId.Trim(),
                LotNumber: string.IsNullOrWhiteSpace(input.LotNumber) ? null : input.LotNumber!.Trim(),
                SerialIds: input.SerialIds,
                ToStockStatus: toStockStatus.ToString(),
                SourceModule: GrnMovementSource.SourceModule,
                SourceType: GrnMovementSource.SourceType,
                SourceDocumentId: grnId,
                SourceLineId: poLineId);

            var posted = await _inventoryClient.PostMovementAsync(movement, cancellationToken);

            grnLines.Add(new GrnLineEntity
            {
                PoLineId = poLineId,
                ItemId = input.ItemId.Trim(),
                SkuId = input.SkuId.Trim(),
                SkuLevel = skuLevel,
                LotNumber = string.IsNullOrWhiteSpace(input.LotNumber) ? null : input.LotNumber!.Trim(),
                SerialIds = input.SerialIds,
                Quantity = quantity,
                UomId = input.UomId.Trim(),
                ToStockStatus = toStockStatus,
                // SALT REFERANS — INVENTORY (MOD-0173) gerçeği; GRN balance DEĞİL.
                InventoryTransactionId = posted.TransactionId,
                LotId = posted.LotId
            });

            events.Add(new GoodsReceivedEventEntity
            {
                GrnId = grnId,
                PoId = poId,
                ItemId = input.ItemId.Trim(),
                SkuId = input.SkuId.Trim(),
                Quantity = quantity,
                InventoryTransactionId = posted.TransactionId,
                ReceivedAt = receivedAt
            });
        }

        var entity = new GoodsReceiptEntity
        {
            GrnId = grnId,
            PoId = poId,
            WarehouseId = warehouseId,
            LocationId = locationId,
            Status = GrnStatusEnum.Posted, // başarılı INVENTORY post sonrası Posted
            Lines = grnLines,
            EmittedEvents = events, // GoodsReceived — gömülü append-only outbox (ASSUMPTION-GRN-EVENT)
            ReceivedAt = receivedAt,
            SourceSystem = string.IsNullOrWhiteSpace(request.SourceSystem) ? null : request.SourceSystem!.Trim(),
            ExternalRef = string.IsNullOrWhiteSpace(request.ExternalRef) ? null : request.ExternalRef!.Trim(),
            IdempotencyKey = idempotencyKey
        };

        var created = await _repository.CreateAsync(entity, cancellationToken);
        return Response<GrnResponseDto>.Success(GrnMapping.ToDto(created), 201);
    }

    private static string GenerateGrnId()
        => "GRN-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();

    /// <summary>Satır bazlı INVENTORY idempotency anahtarı — (grn key/id, satır index) çifti; stabil ve replay-safe.</summary>
    private static string LineMovementKey(string root, int lineIndex)
        => $"{root}#{lineIndex}";
}
