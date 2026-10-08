using Diten.PlanningService.Domain.Features.DemandPlanning;
using MediatR;

namespace Diten.PlanningService.Application.Features.DemandPlanning.HistoryImports;

public sealed record ReviewHistoryImportBatchCommand(
    Guid TenantId, Guid ActorId, Guid BatchId, Guid SelectedLegalEntityHint,
    bool HasReviewPermission,
    ImportBatchReviewState Decision, string Reason, string IdempotencyKey)
    : IRequest<Response<HistoryImportBatchResult>>;
