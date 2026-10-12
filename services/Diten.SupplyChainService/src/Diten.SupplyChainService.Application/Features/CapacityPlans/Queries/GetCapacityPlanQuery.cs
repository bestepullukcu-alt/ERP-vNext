using MediatR;
using Diten.SupplyChainService.Application.Common;
namespace Diten.SupplyChainService.Application.Features.CapacityPlans.Queries;
public sealed record GetCapacityPlanQuery(Guid CapacityPlanId) : IRequest<Response<CapacityPlanResponse>>;
