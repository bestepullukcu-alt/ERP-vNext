using MediatR;
using Diten.SupplyChainService.Application.Common;
namespace Diten.SupplyChainService.Application.Features.SupplierPerformance.Queries;
public sealed record GetSupplierEvaluationQuery(Guid EvaluationId) : IRequest<Response<EvaluationResponse>>;
