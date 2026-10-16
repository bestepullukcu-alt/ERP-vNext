using Diten.PlanningService.Domain.Features.DemandPlanning;

namespace Diten.PlanningService.Application.Features.DemandPlanning;

public sealed record ForecastDraftReplay(ForecastRun Run, DemandRevisionDraft Draft);

public interface IForecastDraftStore
{
    Task<ForecastDraftReplay?> FindByRequestAsync(Guid tenantId, Guid legalEntityId,
        Guid planningCycleId, Guid actorId, string requestKey,
        CancellationToken cancellationToken);

    Task<ManualDraftStoreResult> CreateForecastAsync(DemandRevisionDraft draft,
        string requestKey, ForecastRun run, ForecastSelectionAudit selection,
        CancellationToken cancellationToken);
}