namespace Diten.PlanningService.Application.Features.DemandPlanning.Contracts;

// No verified live dispatch producer exists at this stage. A null result means unknown, never zero demand.
public interface IDispatchHistoryReader
{
    Task<bool?> HasVerifiedCoverageAsync(Guid tenantId, Guid legalEntityId, string warehouseId, DateOnly from, DateOnly through,
        CancellationToken cancellationToken);
}