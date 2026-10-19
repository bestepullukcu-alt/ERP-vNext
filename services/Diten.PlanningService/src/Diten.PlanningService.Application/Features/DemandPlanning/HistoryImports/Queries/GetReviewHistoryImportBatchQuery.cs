using MediatR;

namespace Diten.PlanningService.Application.Features.DemandPlanning.HistoryImports;

public sealed record GetReviewHistoryImportBatchQuery(
    Guid TenantId, Guid ActorId, Guid BatchId, Guid SelectedLegalEntityHint,
    bool HasReviewPermission)
    : IRequest<Response<HistoryImportReviewView>>;
