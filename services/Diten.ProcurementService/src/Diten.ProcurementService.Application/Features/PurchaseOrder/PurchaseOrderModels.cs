using Diten.ProcurementService.Domain.Entities;

namespace Diten.ProcurementService.Application.Features.PurchaseOrder;

// ── Contract version sabiti (REQUISITION-PO owned contract; requisition-po.openapi.yaml) ──
public static class PurchaseOrderContract
{
    public const string Version = "v1";
}

// ══ READ DTOs (requisition-po.openapi.yaml şekilleriyle uyumlu) ═══════════════════

/// <summary>PoLine read DTO — contract PoLine {poLineId, itemId, skuId?, quantity, uomId, unitPrice, lineAmount}.</summary>
public sealed record PoLineDto(
    string PoLineId,
    string ItemId,
    string? SkuId,
    string Quantity,
    string UomId,
    string UnitPrice,
    string LineAmount);

/// <summary>PurchaseOrder read DTO — contract PurchaseOrder. (Id/Version house-style zarf alanı; concurrency için.)</summary>
public sealed record PurchaseOrderDto(
    Guid Id,
    string PoId,
    string SupplierId,
    string? RequisitionId,
    PoStatus Status,
    string Currency,
    IReadOnlyList<PoLineDto> Lines,
    string TotalAmount,
    string? WorkflowInstanceId,
    int Version,
    string ContractVersion);

/// <summary>Sayfalı liste sonucu — contract listPurchaseOrders {items, nextCursor, contractVersion}.</summary>
public sealed record PurchaseOrderListResultDto(
    IReadOnlyList<PurchaseOrderDto> Items,
    string? NextCursor,
    string ContractVersion);

// ══ REQUEST / INPUT records (controller body binding — contract PurchaseOrderUpsert) ══

/// <summary>contract PoLine request satırı (poLineId + lineAmount server-computed; girdi değil).</summary>
public sealed record PoLineInput(
    string ItemId,
    string? SkuId,
    string Quantity,
    string UomId,
    string UnitPrice);

/// <summary>contract PurchaseOrderUpsert request body.</summary>
public sealed record PurchaseOrderUpsertRequest(
    string SupplierId,
    string? RequisitionId,
    string Currency,
    List<PoLineInput>? Lines,
    string? SourceSystem,
    string? ExternalRef);
