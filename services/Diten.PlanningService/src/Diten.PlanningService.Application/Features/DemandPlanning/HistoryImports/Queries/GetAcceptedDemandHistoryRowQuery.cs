using MediatR;

namespace Diten.PlanningService.Application.Features.DemandPlanning.HistoryImports;

public sealed record GetAcceptedDemandHistoryRowQuery(
    Guid TenantId, Guid ActorId, Guid AcceptedRowId,
    Guid SelectedLegalEntityHint, bool HasReadPermission)
    : IRequest<Response<AcceptedDemandHistoryView>>;
