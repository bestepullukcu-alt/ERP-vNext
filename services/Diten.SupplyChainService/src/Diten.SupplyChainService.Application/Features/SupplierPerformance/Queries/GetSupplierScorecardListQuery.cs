using MediatR;
using Diten.SupplyChainService.Application.Common;
namespace Diten.SupplyChainService.Application.Features.SupplierPerformance.Queries;
public sealed record GetSupplierScorecardListQuery(string? SupplierId, string? PeriodFrom, string? PeriodTo, string? RiskLevel) : IRequest<Response<ScorecardListResponse>>;
