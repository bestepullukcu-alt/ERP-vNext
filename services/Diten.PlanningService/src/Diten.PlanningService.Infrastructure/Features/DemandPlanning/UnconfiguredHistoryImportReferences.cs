using Diten.PlanningService.Application.Features.DemandPlanning.HistoryImports;

namespace Diten.PlanningService.Infrastructure.Features.DemandPlanning;

// No live MDM, LOCATION, UoM or dispatch call is permitted in this slice.
public sealed class UnconfiguredHistoryImportReferences :
    IHistoryImportScopeAuthority, IHistoryRowReferenceChecker
{
    public Task<Guid?> ResolveSelectedAsync(Guid tenantId, Guid actorId,
        Guid selectedLegalEntityHint, CancellationToken cancellationToken) =>
        throw new InvalidOperationException("LegalEntity assignment source is not configured.");

    public Task<bool?> IsAuthorizedAsync(Guid tenantId, Guid actorId,
        Guid legalEntityId, DateOnly from, DateOnly through,
        IReadOnlyList<string> warehouses, CancellationToken cancellationToken) =>
        Task.FromResult<bool?>(null);

    public Task<HistoryReferenceResult> CheckAsync(Guid tenantId, Guid legalEntityId,
        Guid skuId, string skuLevel, string warehouseId, DateOnly occurredOn,
        string originalUomId, CancellationToken cancellationToken) =>
        Task.FromResult(new HistoryReferenceResult(
            HistoryReferenceState.Unavailable, null, null, null, null));
}
