using Diten.ProcurementService.Domain.Entities;

namespace Diten.ProcurementService.Application.Features.Grn;

// ── Contract version sabiti (GRN-EVENT owned-frozen contract; grn-event.openapi.yaml) ──
public static class GrnContract
{
    public const string Version = "v1";
}

/// <summary>
/// INVENTORY-BUNDLE canonical MovementType değerleri (CONSUMED — redefine değil, yalnız çağrı için sabit string).
/// GRN yalnız mal-kabul + düzeltme hareketlerini kullanır.
/// </summary>
public static class InventoryMovementTypes
{
    public const string GoodsReceiptPo = "GOODS_RECEIPT_PO";
    public const string SupplierReturn = "SUPPLIER_RETURN";
    public const string Reversal = "REVERSAL";
}

/// <summary>INVENTORY MovementRequest source alanları (contract örneğiyle uyumlu — MOD-0142 §19).</summary>
public static class GrnMovementSource
{
    public const string SourceModule = "MOD-0142";
    public const string SourceType = "GOODS_RECEIPT";
}

// ══ READ DTOs (grn-event.openapi.yaml şekilleriyle uyumlu) ═══════════════════════════

/// <summary>GrnResponse.lines[] öğesi — contract {poLineId, inventoryTransactionId, lotId?, quantity}.</summary>
public sealed record GrnLineResultDto(
    string? PoLineId,
    string InventoryTransactionId,
    string? LotId,
    string Quantity);

/// <summary>GrnResponse read DTO — contract GrnResponse {grnId, poId?, status, lines[], receivedAt, contractVersion}.
/// (Id/Version house-style zarf alanı; concurrency/reverse için.)</summary>
public sealed record GrnResponseDto(
    Guid Id,
    string GrnId,
    string? PoId,
    GrnStatus Status,
    IReadOnlyList<GrnLineResultDto> Lines,
    DateTimeOffset ReceivedAt,
    int Version,
    string ContractVersion);

/// <summary>Sayfalı liste sonucu (pack §3 GetGrnList; contract-additive zarf).</summary>
public sealed record GrnListResultDto(
    IReadOnlyList<GrnResponseDto> Items,
    string? NextCursor,
    string ContractVersion);

// ══ REQUEST / INPUT records (controller body binding — contract GrnRequest/GrnLine) ══

/// <summary>contract GrnLine request satırı (inventoryTransactionId/lotId server-computed; girdi değil).</summary>
public sealed record GrnLineInput(
    string? PoLineId,
    string ItemId,
    string SkuId,
    string SkuLevel,
    string? LotNumber,
    List<string>? SerialIds,
    string Quantity,
    string UomId,
    string ToStockStatus);

/// <summary>contract GrnRequest body. idempotencyKey teknik header; TenantId/LegalEntityId server-resolved (payload'da YOK).</summary>
public sealed record GrnRequestBody(
    string? PoId,
    string WarehouseId,
    string? LocationId,
    List<GrnLineInput>? Lines,
    string? SourceSystem,
    string? ExternalRef);
