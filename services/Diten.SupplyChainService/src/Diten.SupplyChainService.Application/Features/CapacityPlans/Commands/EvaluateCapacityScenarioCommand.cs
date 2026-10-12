using MediatR;
using Diten.SupplyChainService.Application.Common;
namespace Diten.SupplyChainService.Application.Features.CapacityPlans.Commands;
public sealed record EvaluateCapacityScenarioCommand(Guid CapacityPlanId, Guid ScenarioId, EvaluateCapacityScenarioRequest Body) : IRequest<Response<CapacityEvaluationResponse>>;
