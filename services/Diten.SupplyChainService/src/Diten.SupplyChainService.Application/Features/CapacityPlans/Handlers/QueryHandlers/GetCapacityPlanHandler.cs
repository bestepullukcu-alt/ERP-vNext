using MediatR;
using Diten.SupplyChainService.Application.Common;
using Diten.SupplyChainService.Application.Features.CapacityPlans.Queries;
using Diten.SupplyChainService.Domain.Features.CapacityPlans;
namespace Diten.SupplyChainService.Application.Features.CapacityPlans.Handlers.QueryHandlers;
public sealed class GetCapacityPlanHandler(ICapacityRepository repository, CapacityRequestContext context) : IRequestHandler<GetCapacityPlanQuery,Response<CapacityPlanResponse>>
{
    public async Task<Response<CapacityPlanResponse>> Handle(GetCapacityPlanQuery request,CancellationToken ct)
    {
        try
        {
        var value=await repository.GetPlanAsync(context.Scope,request.CapacityPlanId,ct);
        return value is null ? Response<CapacityPlanResponse>.Fail("UNKNOWN_CAPACITY_PLAN",404) : Response<CapacityPlanResponse>.Success(CapacityProjection.Plan(value),200);
        }
        catch(CapacityReadUnavailableException) { return Response<CapacityPlanResponse>.Fail("DEPENDENCY_UNAVAILABLE",503); }
    }
}
