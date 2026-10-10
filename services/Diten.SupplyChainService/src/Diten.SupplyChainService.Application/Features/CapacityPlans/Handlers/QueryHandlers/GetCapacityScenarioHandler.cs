using MediatR;
using Diten.SupplyChainService.Application.Common;
using Diten.SupplyChainService.Application.Features.CapacityPlans.Queries;
using Diten.SupplyChainService.Domain.Features.CapacityPlans;
namespace Diten.SupplyChainService.Application.Features.CapacityPlans.Handlers.QueryHandlers;
public sealed class GetCapacityScenarioHandler(ICapacityRepository repository, CapacityRequestContext context) : IRequestHandler<GetCapacityScenarioQuery,Response<CapacityScenarioResponse>>
{
    public async Task<Response<CapacityScenarioResponse>> Handle(GetCapacityScenarioQuery request,CancellationToken ct)
    {
        try
        {
        var value=await repository.GetScenarioAsync(context.Scope,request.CapacityPlanId,request.ScenarioId,ct);
        return value is null ? Response<CapacityScenarioResponse>.Fail("UNKNOWN_CAPACITY_SCENARIO",404) : Response<CapacityScenarioResponse>.Success(CapacityProjection.Scenario(value),200);
        }
        catch(CapacityReadUnavailableException) { return Response<CapacityScenarioResponse>.Fail("DEPENDENCY_UNAVAILABLE",503); }
    }
}
