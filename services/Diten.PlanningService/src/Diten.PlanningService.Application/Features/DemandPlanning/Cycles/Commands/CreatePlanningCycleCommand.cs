using MediatR;

namespace Diten.PlanningService.Application.Features.DemandPlanning.Cycles;

public sealed record CreatePlanningCycleCommand(
    Guid TenantId, Guid ActorId, Guid SelectedLegalEntityHint,
    DateOnly AsOfDate, DateOnly FirstWeekStart, string IdempotencyKey)
    : IRequest<Response<PlanningCycleResult>>;
