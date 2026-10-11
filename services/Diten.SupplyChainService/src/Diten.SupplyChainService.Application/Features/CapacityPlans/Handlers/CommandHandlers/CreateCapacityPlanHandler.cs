using MediatR;
using Diten.SupplyChainService.Application.Common;
using Diten.SupplyChainService.Application.Features.CapacityPlans.Commands;
using Diten.SupplyChainService.Domain.Features.CapacityPlans;
namespace Diten.SupplyChainService.Application.Features.CapacityPlans.Handlers.CommandHandlers;
public sealed class CreateCapacityPlanHandler(ICapacityRepository repository, CapacityRequestContext context) : IRequestHandler<CreateCapacityPlanCommand,Response<CapacityPlanResponse>>
{
    public async Task<Response<CapacityPlanResponse>> Handle(CreateCapacityPlanCommand request, CancellationToken ct)
    {
        var b=request.Body;
        var plan=new CapacityPlan { Id=Guid.NewGuid(), TenantId=context.Scope.TenantId, LegalEntityId=context.Scope.LegalEntityId, CreatedBy=context.Scope.ActorId, Version=1, CreatedAt=DateTimeOffset.UtcNow, Name=b.Name!, HorizonStart=b.HorizonStart,HorizonEnd=b.HorizonEnd,DemandPlanId=b.DemandPlanId!,DemandPlanVersion=b.DemandPlanVersion!,SourceCapturedAt=b.SourceCapturedAt,SourceChecksum=b.SourceChecksum! };
        var result=await repository.CreatePlanAsync(context.Scope,context.IdempotencyKey,CapacityRequestFingerprint.Create("createCapacityPlan","create",b),context.CorrelationId,plan,ct);
        return result.ErrorCode is null ? Response<CapacityPlanResponse>.Success(CapacityProjection.Plan(result.Value!),result.StatusCode) : Response<CapacityPlanResponse>.Fail(result.ErrorCode,result.StatusCode);
    }
}
