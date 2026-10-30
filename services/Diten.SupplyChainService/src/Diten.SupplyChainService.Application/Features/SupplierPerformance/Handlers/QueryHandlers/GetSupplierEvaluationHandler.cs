using MediatR;
using Diten.SupplyChainService.Application.Common;
using Diten.SupplyChainService.Application.Features.SupplierPerformance.Queries;
using Diten.SupplyChainService.Domain.Features.SupplierPerformance;
namespace Diten.SupplyChainService.Application.Features.SupplierPerformance.Handlers.QueryHandlers;
public sealed class GetSupplierEvaluationHandler(IEvaluationRepository repository, SupplierPerformanceRequestContext context) : IRequestHandler<GetSupplierEvaluationQuery, Response<EvaluationResponse>>
{
    public async Task<Response<EvaluationResponse>> Handle(GetSupplierEvaluationQuery request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        // Another tenant's or company's evaluation is the same 404 as a missing one (T-0147-ISO-01).
        return await repository.GetAsync(context.Scope, request.EvaluationId, ct) is { } evaluation
            ? Response<EvaluationResponse>.Success(EvaluationResponse.From(evaluation))
            : Response<EvaluationResponse>.Fail(SupplierPerformanceErrorCodes.UnknownEvaluation, 404);
    }
}
