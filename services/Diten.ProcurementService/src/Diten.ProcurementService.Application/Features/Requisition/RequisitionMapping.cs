using RequisitionEntity = Diten.ProcurementService.Domain.Entities.Requisition;

namespace Diten.ProcurementService.Application.Features.Requisition;

/// <summary>Entity → DTO eşlemeleri (command + query handler'ları paylaşır). Tek kaynak; şekil sapması önlenir.</summary>
internal static class RequisitionMapping
{
    public static RequisitionDto ToDto(RequisitionEntity e) => new(
        e.Id,
        e.RequisitionId,
        e.Status,
        e.Lines.Select(l => new RequisitionLineDto(l.ItemId, l.SkuId, l.Quantity, l.UomId, l.NeedBy)).ToList(),
        e.Justification,
        e.WorkflowInstanceId,
        e.Version,
        RequisitionContract.Version);
}
