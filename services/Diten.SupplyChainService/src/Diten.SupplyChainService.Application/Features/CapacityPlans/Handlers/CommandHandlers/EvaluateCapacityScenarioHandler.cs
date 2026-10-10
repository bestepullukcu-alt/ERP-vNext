using MediatR;
using Diten.SupplyChainService.Application.Common;
using Diten.SupplyChainService.Application.Features.CapacityPlans.Commands;
using Diten.SupplyChainService.Domain.Features.CapacityPlans;
namespace Diten.SupplyChainService.Application.Features.CapacityPlans.Handlers.CommandHandlers;
public sealed class EvaluateCapacityScenarioHandler(ICapacityRepository repository, CapacityRequestContext context) : IRequestHandler<EvaluateCapacityScenarioCommand,Response<CapacityEvaluationResponse>>
{
    public async Task<Response<CapacityEvaluationResponse>> Handle(EvaluateCapacityScenarioCommand request, CancellationToken ct)
    {
        var b=request.Body;
        var evaluation=new CapacityEvaluation { Id=Guid.NewGuid(),TenantId=context.Scope.TenantId,LegalEntityId=context.Scope.LegalEntityId,Version=1,CreatedAt=DateTimeOffset.UtcNow,SubmittedAt=DateTimeOffset.UtcNow,CapacityPlanId=request.CapacityPlanId,ScenarioId=request.ScenarioId,EvaluationMode=b.EvaluationMode!,ResourceRefs=b.ResourceRefs!,CorrelationId=context.CorrelationId };
        var result=await repository.EvaluateAsync(context.Scope,request.CapacityPlanId,request.ScenarioId,context.IdempotencyKey,CapacityRequestFingerprint.Create("evaluateCapacityScenario",request.CapacityPlanId+"/"+request.ScenarioId,b),context.CorrelationId,evaluation,ct);
        return result.ErrorCode is null ? Response<CapacityEvaluationResponse>.Success(CapacityProjection.Evaluation(result.Value!),result.StatusCode) : Response<CapacityEvaluationResponse>.Fail(result.ErrorCode,result.StatusCode);
    }
}
