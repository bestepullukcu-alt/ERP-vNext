using ContractEntity = Diten.ProcurementService.Domain.Entities.Contract;

namespace Diten.ProcurementService.Application.Features.Contract;

/// <summary>Entity → DTO eşlemeleri (command + query handler'ları paylaşır). Tek kaynak; şekil sapması önlenir.</summary>
internal static class ContractMapping
{
    public static ContractDto ToDto(ContractEntity e) => new(
        e.Id,
        e.ContractId,
        e.SupplierId,
        e.RfxId,
        e.Title,
        e.Status,
        e.EffectiveFrom,
        e.EffectiveTo,
        e.Currency,
        e.Clauses.Select(c => new ClauseRefDto(c.ClauseId, c.Deviation, c.DeviationText)).ToList(),
        e.WorkflowInstanceId,
        e.EvidenceRefs.ToList(),
        e.Version,
        ContractingContract.Version);
}
