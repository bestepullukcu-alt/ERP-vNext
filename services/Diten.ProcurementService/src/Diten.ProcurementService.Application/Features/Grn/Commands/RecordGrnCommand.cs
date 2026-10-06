using Diten.Shared.Core;
using MediatR;

namespace Diten.ProcurementService.Application.Features.Grn.Commands;

/// <summary>
/// recordGrn (contract POST /api/grn). Tenant/LE server-resolved — payload'da YOK. Idempotency-Key ile idempotent
/// (replay → aynı GRN, mükerrer INVENTORY hareketi YOK). line.itemId MOD-0290'da (seam, fail-closed → 422
/// UNKNOWN_ITEM). Her satır frozen INVENTORY-BUNDLE POST /movements (GOODS_RECEIPT_PO) ile post edilir ve dönen
/// inventoryTransactionId saklanır (SHADOW STOCK YASAK). poId verilirse mevcut olmalı (aksi 422). ≥1 satır +
/// Decimal/float + enum → 422.
/// </summary>
public sealed record RecordGrnCommand(
    string? PoId,
    string WarehouseId,
    string? LocationId,
    List<GrnLineInput>? Lines,
    string? SourceSystem,
    string? ExternalRef,
    string? IdempotencyKey) : IRequest<Response<GrnResponseDto>>;
