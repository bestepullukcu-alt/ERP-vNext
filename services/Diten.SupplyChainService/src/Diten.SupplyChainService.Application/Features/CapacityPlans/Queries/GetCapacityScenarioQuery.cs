using MediatR;
using Diten.SupplyChainService.Application.Common;
namespace Diten.SupplyChainService.Application.Features.CapacityPlans.Queries;
public sealed record GetCapacityScenarioQuery(Guid CapacityPlanId, Guid ScenarioId) : IRequest<Response<CapacityScenarioResponse>>;
