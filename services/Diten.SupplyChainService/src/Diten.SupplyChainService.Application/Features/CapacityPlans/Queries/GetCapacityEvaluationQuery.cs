using MediatR;
using Diten.SupplyChainService.Application.Common;
namespace Diten.SupplyChainService.Application.Features.CapacityPlans.Queries;
public sealed record GetCapacityEvaluationQuery(Guid CapacityPlanId, Guid EvaluationId) : IRequest<Response<CapacityEvaluationResponse>>;
