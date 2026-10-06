using Diten.ProcurementService.Application.Features.Contract;
using ClauseEntity = Diten.ProcurementService.Domain.Entities.Clause;

namespace Diten.ProcurementService.Application.Features.Clause;

/// <summary>Clause entity → DTO eşlemesi (command + query handler'ları paylaşır). contractVersion CONTRACTING
/// contract sürümüdür (Contract feature ile tek kaynak).</summary>
internal static class ClauseMapping
{
    public static ClauseDto ToDto(ClauseEntity e) => new(
        e.Id,
        e.ClauseId,
        e.Category,
        e.Title,
        e.Body,
        e.Version,
        ContractingContract.Version);
}
