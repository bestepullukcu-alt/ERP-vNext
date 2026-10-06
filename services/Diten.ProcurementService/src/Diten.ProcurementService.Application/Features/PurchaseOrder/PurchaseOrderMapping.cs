using PurchaseOrderEntity = Diten.ProcurementService.Domain.Entities.PurchaseOrder;

namespace Diten.ProcurementService.Application.Features.PurchaseOrder;

/// <summary>Entity → DTO eşlemeleri (command + query handler'ları paylaşır). Tek kaynak; şekil sapması önlenir.</summary>
internal static class PurchaseOrderMapping
{
    public static PurchaseOrderDto ToDto(PurchaseOrderEntity e) => new(
        e.Id,
        e.PoId,
        e.SupplierId,
        e.RequisitionId,
        e.Status,
        e.Currency,
        e.Lines.Select(l => new PoLineDto(l.PoLineId, l.ItemId, l.SkuId, l.Quantity, l.UomId, l.UnitPrice, l.LineAmount)).ToList(),
        e.TotalAmount,
        e.WorkflowInstanceId,
        e.Version,
        PurchaseOrderContract.Version);
}
