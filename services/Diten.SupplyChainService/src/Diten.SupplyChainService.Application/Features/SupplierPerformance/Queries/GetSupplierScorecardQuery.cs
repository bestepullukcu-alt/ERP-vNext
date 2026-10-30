using MediatR;
using Diten.SupplyChainService.Application.Common;
namespace Diten.SupplyChainService.Application.Features.SupplierPerformance.Queries;
public sealed record GetSupplierScorecardQuery(Guid ScorecardId) : IRequest<Response<ScorecardResponse>>;
