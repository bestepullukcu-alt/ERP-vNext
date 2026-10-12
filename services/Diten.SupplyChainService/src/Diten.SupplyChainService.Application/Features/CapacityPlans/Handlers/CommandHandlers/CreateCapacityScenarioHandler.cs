using MediatR;
using Diten.SupplyChainService.Application.Common;
using Diten.SupplyChainService.Application.Features.CapacityPlans.Commands;
using Diten.SupplyChainService.Domain.Features.CapacityPlans;
namespace Diten.SupplyChainService.Application.Features.CapacityPlans.Handlers.CommandHandlers;
public sealed class CreateCapacityScenarioHandler(ICapacityRepository repository, CapacityRequestContext context) : IRequestHandler<CreateCapacityScenarioCommand,Response<CapacityScenarioResponse>>
{
    public async Task<Response<CapacityScenarioResponse>> Handle(CreateCapacityScenarioCommand request, CancellationToken ct)
    {
        var b=request.Body;
        var scenario=new CapacityScenario { Id=Guid.NewGuid(),TenantId=context.Scope.TenantId,LegalEntityId=context.Scope.LegalEntityId,CreatedBy=context.Scope.ActorId,Version=1,CreatedAt=DateTimeOffset.UtcNow,CapacityPlanId=request.CapacityPlanId,Name=b.Name!,ConstraintRefs=b.ConstraintRefs!,Adjustments=b.Adjustments! };
        var result=await repository.CreateScenarioAsync(context.Scope,request.CapacityPlanId,context.IdempotencyKey,CapacityRequestFingerprint.Create("createCapacityScenario",request.CapacityPlanId.ToString(),b),context.CorrelationId,scenario,ct);
        return result.ErrorCode is null ? Response<CapacityScenarioResponse>.Success(CapacityProjection.Scenario(result.Value!),result.StatusCode) : Response<CapacityScenarioResponse>.Fail(result.ErrorCode,result.StatusCode);
    }
}
