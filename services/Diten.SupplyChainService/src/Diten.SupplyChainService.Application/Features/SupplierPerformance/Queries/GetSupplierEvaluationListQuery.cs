using MediatR;
using Diten.SupplyChainService.Application.Common;
namespace Diten.SupplyChainService.Application.Features.SupplierPerformance.Queries;
// Query values arrive as text so a malformed one is a contract 400 from the validator, not a model-binding error.
public sealed record GetSupplierEvaluationListQuery(string? SupplierId, string? Status, string? PeriodFrom, string? PeriodTo, string? Page, string? PageSize) : IRequest<Response<EvaluationListResponse>>;
