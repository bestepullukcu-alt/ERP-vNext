using MediatR;
using Diten.SupplyChainService.Application.Common;
using Diten.SupplyChainService.Application.Features.CapacityPlans.Queries;
using Diten.SupplyChainService.Domain.Features.CapacityPlans;
namespace Diten.SupplyChainService.Application.Features.CapacityPlans.Handlers.QueryHandlers;
public sealed class GetCapacityEvaluationHandler(ICapacityRepository repository, CapacityRequestContext context) : IRequestHandler<GetCapacityEvaluationQuery,Response<CapacityEvaluationResponse>>
{
    public async Task<Response<CapacityEvaluationResponse>> Handle(GetCapacityEvaluationQuery request,CancellationToken ct)
    {
        try
        {
        var value=await repository.GetEvaluationAsync(context.Scope,request.CapacityPlanId,request.EvaluationId,ct);
        return value is null ? Response<CapacityEvaluationResponse>.Fail("UNKNOWN_CAPACITY_EVALUATION",404) : Response<CapacityEvaluationResponse>.Success(CapacityProjection.Evaluation(value),200);
        }
        catch(CapacityReadUnavailableException) { return Response<CapacityEvaluationResponse>.Fail("DEPENDENCY_UNAVAILABLE",503); }
    }
}
