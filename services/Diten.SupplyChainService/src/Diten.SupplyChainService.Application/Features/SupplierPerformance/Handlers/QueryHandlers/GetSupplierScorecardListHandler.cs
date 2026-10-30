using MediatR;
using Diten.SupplyChainService.Application.Common;
using Diten.SupplyChainService.Application.Features.SupplierPerformance.Queries;
using Diten.SupplyChainService.Domain.Features.SupplierPerformance;
namespace Diten.SupplyChainService.Application.Features.SupplierPerformance.Handlers.QueryHandlers;
public sealed class GetSupplierScorecardListHandler(IEvaluationRepository repository, SupplierPerformanceRequestContext context) : IRequestHandler<GetSupplierScorecardListQuery, Response<ScorecardListResponse>>
{
    public async Task<Response<ScorecardListResponse>> Handle(GetSupplierScorecardListQuery request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var rows = await repository.ListPublishedAsync(context.Scope, new(request.SupplierId, request.PeriodFrom, request.PeriodTo, request.RiskLevel), ct);
        var items = rows.Select(SupplierScorecard.From).OfType<SupplierScorecard>().Select(ScorecardResponse.From).ToList();
        return Response<ScorecardListResponse>.Success(new(items, items.Count, context.CorrelationId));
    }
}
