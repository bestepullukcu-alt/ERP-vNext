using Diten.PlanningService.Application.Features.DemandPlanning;
using MongoDB.Driver;

namespace Diten.PlanningService.Persistence.Features.DemandPlanning;

public sealed partial class ManualDraftMongoStore
{
    public async Task<ForecastDraftReplay?> FindByRequestAsync(Guid tenantId,
        Guid legalEntityId, Guid planningCycleId, Guid actorId,
        string requestKey, CancellationToken cancellationToken)
    {
        if (tenantId == Guid.Empty || legalEntityId == Guid.Empty ||
            planningCycleId == Guid.Empty || actorId == Guid.Empty ||
            string.IsNullOrWhiteSpace(requestKey))
            throw new ArgumentException("Complete scoped forecast request is required.");
        var run = await context.ForecastRuns.Find(x =>
            x.TenantId == tenantId && x.LegalEntityId == legalEntityId &&
            x.PlanningCycleId == planningCycleId && x.SelectedBy == actorId &&
            x.RequestKey == requestKey && !x.IsDeleted)
            .FirstOrDefaultAsync(cancellationToken);
        if (run is null) return null;
        var audit = await context.ForecastSelectionAudit.Find(x =>
            x.TenantId == tenantId && x.LegalEntityId == legalEntityId &&
            x.ForecastRunId == run.Id && x.RevisionId == run.RevisionId &&
            x.RequestKey == run.RequestKey &&
            x.RequestFingerprint == run.RequestFingerprint).ToListAsync(cancellationToken);
        if (audit.Count != 1 || audit[0].ActorId != actorId ||
            audit[0].SkuId != run.SkuId || audit[0].WarehouseId != run.WarehouseId ||
            audit[0].NewMethod != run.Method || audit[0].PolicyVersion != run.PolicyVersion)
            throw new InvalidDataException("Forecast selection audit is incomplete.");
        var draft = await ReadAsync(tenantId, legalEntityId, run.RevisionId,
            cancellationToken) ?? throw new InvalidDataException("Forecast Draft is missing.");
        if (draft.PlanningCycleId != run.PlanningCycleId ||
            draft.CreatedBy != run.SelectedBy ||
            draft.Series.Count != 1 ||
            draft.Series[0].SkuId != run.SkuId ||
            draft.Series[0].WarehouseId != run.WarehouseId ||
            draft.Series[0].Weeks.Any(week =>
                week.MethodProvenance?.ForecastRunId != run.Id))
            throw new InvalidDataException("Forecast Run and Draft disagree.");
        return new(run, draft);
    }
}