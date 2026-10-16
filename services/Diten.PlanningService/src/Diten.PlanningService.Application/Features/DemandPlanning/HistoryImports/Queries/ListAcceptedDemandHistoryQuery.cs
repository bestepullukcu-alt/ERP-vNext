using MediatR;

namespace Diten.PlanningService.Application.Features.DemandPlanning.HistoryImports;

public sealed record ListAcceptedDemandHistoryQuery(
    Guid TenantId, Guid ActorId, Guid SelectedLegalEntityHint,
    bool HasReadPermission, int Skip, int Take)
    : IRequest<Response<IReadOnlyList<AcceptedDemandHistoryView>>>;
