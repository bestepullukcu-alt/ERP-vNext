using Diten.PlanningService.Domain.Features.DemandPlanning;
using MediatR;

namespace Diten.PlanningService.Application.Features.DemandPlanning.HistoryImports;

public sealed record AcceptDemandHistoryRowsCommand(
    Guid TenantId, Guid ActorId, Guid BatchId, int RowNumber,
    Guid SelectedLegalEntityHint, bool HasAcceptPermission,
    string Reason, string IdempotencyKey)
    : IRequest<Response<AcceptedDemandHistoryRow>>;
