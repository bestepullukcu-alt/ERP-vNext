using MediatR;
using Diten.SupplyChainService.Application.Common;
namespace Diten.SupplyChainService.Application.Features.CapacityPlans.Commands;
public sealed record CreateCapacityPlanCommand(CreateCapacityPlanRequest Body) : IRequest<Response<CapacityPlanResponse>>;
