using MediatR;
using Diten.SupplyChainService.Application.Common;
namespace Diten.SupplyChainService.Application.Features.CapacityPlans.Commands;
public sealed record CreateCapacityScenarioCommand(Guid CapacityPlanId, CreateCapacityScenarioRequest Body) : IRequest<Response<CapacityScenarioResponse>>;
