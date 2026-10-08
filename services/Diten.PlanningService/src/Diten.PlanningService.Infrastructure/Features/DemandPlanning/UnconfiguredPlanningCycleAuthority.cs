using Diten.PlanningService.Application.Features.DemandPlanning.Cycles;

namespace Diten.PlanningService.Infrastructure.Features.DemandPlanning;

// The live LegalEntity/time-zone/calendar producer is not yet verified.
// Never infer its scope or interpret an outage as permission to use ISO fallback.
public sealed class UnconfiguredPlanningCycleAuthority : IPlanningCycleAuthority
{
    public Task<Guid?> ResolveSelectedAsync(Guid tenantId, Guid actorId,
        Guid selectedLegalEntityHint, CancellationToken cancellationToken) =>
        throw new InvalidOperationException("LegalEntity assignment source is not configured.");

    public Task<AuthorizedPlanningContext?> ResolveAsync(
        Guid tenantId, Guid legalEntityId, DateOnly asOfDate,
        DateOnly firstWeekStart, CancellationToken cancellationToken) =>
        throw new InvalidOperationException("Authorized LegalEntity/calendar contract fixture is not configured.");
}
