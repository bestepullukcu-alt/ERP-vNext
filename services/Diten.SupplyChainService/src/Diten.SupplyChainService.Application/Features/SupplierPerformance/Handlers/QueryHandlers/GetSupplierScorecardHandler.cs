using MediatR;
using Diten.SupplyChainService.Application.Common;
using Diten.SupplyChainService.Application.Features.SupplierPerformance.Queries;
using Diten.SupplyChainService.Domain.Features.SupplierPerformance;
namespace Diten.SupplyChainService.Application.Features.SupplierPerformance.Handlers.QueryHandlers;
public sealed class GetSupplierScorecardHandler(IEvaluationRepository repository, SupplierPerformanceRequestContext context) : IRequestHandler<GetSupplierScorecardQuery, Response<ScorecardResponse>>
{
    public async Task<Response<ScorecardResponse>> Handle(GetSupplierScorecardQuery request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        return await repository.GetPublishedAsync(context.Scope, request.ScorecardId, ct) is { } evaluation && SupplierScorecard.From(evaluation) is { } scorecard
            ? Response<ScorecardResponse>.Success(ScorecardResponse.From(scorecard))
            : Response<ScorecardResponse>.Fail(SupplierPerformanceErrorCodes.UnknownScorecard, 404);
    }
}
