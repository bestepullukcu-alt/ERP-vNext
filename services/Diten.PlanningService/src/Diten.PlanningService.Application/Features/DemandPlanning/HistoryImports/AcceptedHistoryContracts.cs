using Diten.PlanningService.Domain.Features.DemandPlanning;

namespace Diten.PlanningService.Application.Features.DemandPlanning.HistoryImports;

public sealed record AcceptedDemandHistoryView(
    AcceptedDemandHistoryRow Row, DemandHistoryAcceptanceAudit Audit,
    bool? EffectiveUnitVerificationPending = null,
    string? UnitWarning = null);

public enum HistoryAcceptanceOutcome { Created, Existing, Conflict, Ineligible, Unavailable }
public sealed record HistoryAcceptanceResult(HistoryAcceptanceOutcome Outcome,
    AcceptedDemandHistoryRow? Row);

public interface IAcceptedDemandHistoryStore
{
    Task<HistoryAcceptanceResult> AcceptAsync(DemandHistoryImportBatch expectedBatch,
        AcceptedDemandHistoryRow row, DemandHistoryAcceptanceAudit audit,
        CancellationToken cancellationToken);
    Task<AcceptedDemandHistoryView?> GetAsync(Guid tenantId, Guid legalEntityId,
        Guid acceptedRowId, CancellationToken cancellationToken);
    Task<IReadOnlyList<AcceptedDemandHistoryView>> ListAsync(Guid tenantId, Guid legalEntityId,
        int skip, int take, CancellationToken cancellationToken);
}
