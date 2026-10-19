using MediatR;

namespace Diten.PlanningService.Application.Features.DemandPlanning.HistoryImports;

public sealed record GetOwnDemandHistoryImportBatchQuery(
    Guid TenantId, Guid ActorId, Guid BatchId, Guid SelectedLegalEntityHint)
    : IRequest<Response<HistoryImportBatchResult>>;
