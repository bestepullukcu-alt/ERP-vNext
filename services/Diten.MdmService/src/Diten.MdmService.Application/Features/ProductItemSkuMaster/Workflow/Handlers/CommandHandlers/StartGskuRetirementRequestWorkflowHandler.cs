using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Contracts;
using Diten.MdmService.Application.Contracts.Workflow;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Lifecycle;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Handlers.QueryHandlers;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Workflow.Commands;
using Diten.MdmService.Application.Features.ProductLegalEntityScopes;
using Diten.MdmService.Domain.Repositories;
using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.ProductItemSkuMaster.Workflow.Handlers.CommandHandlers;

public sealed class StartGskuRetirementRequestWorkflowHandler(ITenantContext tenant,
    IProductIdentityLifecycleActorContext actor, IProductIdentityDelegatedTokenAccessor tokens,
    IGskuRepository gskus, IProductDefinitionRevisionRepository revisions,
    IProductLegalEntityScopeRolloutStateRepository rollout, IProductLegalEntityScopePolicyRepository policies,
    ProductLegalEntityScopeCandidateFacade candidates, GskuRetirementRequestWorkflowProcessor processor,
    GskuRetirementRequestExecutionConfiguration execution)
    : IRequestHandler<StartGskuRetirementRequestWorkflowCommand, Response<GskuRetirementRequestWorkflowResult>>
{
    public async Task<Response<GskuRetirementRequestWorkflowResult>> Handle(StartGskuRetirementRequestWorkflowCommand command,
        CancellationToken cancellationToken)
    {
        if (!actor.TryResolveCanonicalHumanSubject(out var subjectId)
            || !actor.HasPermission(GskuRetirementRequestPermissions.Request)) return Fail("PRODUCT_IDENTITY_LIFECYCLE_FORBIDDEN", 403);
        string token;
        try { token = tokens.GetRequiredToken(); }
        catch (ProductIdentityDelegatedTokenException e) { return Fail(e.ErrorCode, 401); }
        var gsku = await gskus.GetByIdAsync(command.Request.GskuId, cancellationToken);
        if (gsku is null) return Fail("GSKU_NOT_FOUND", 404);
        var revision = await revisions.GetByIdAsync(gsku.ProductDefinitionRevisionId, cancellationToken);
        if (revision is null) return Fail("GSKU_NOT_FOUND", 404);
        var guard = new ProductLegalEntityScopeConsumerGuard(rollout, policies, candidates, tenant);
        var scope = await guard.ResolveContextAsync(
            ProductLegalEntityScopeConsumerGuard.GlobalProductReadPermission, cancellationToken);
        if (!scope.IsSuccessful) return Fail(scope.FailureCode!, scope.StatusCode);
        if (!(await guard.EvaluateAsync(scope.Context!, revision.GlobalProductId, cancellationToken)).Allowed)
            return Fail("GSKU_NOT_FOUND", 404);
        var r = command.Request;
        var result = await processor.StartInteractiveAsync(tenant.TenantId, r.GskuId, r.ExpectedGskuVersion,
            r.OperationId, subjectId, r.RequestReason, token, execution, cancellationToken);
        if (!result.Succeeded || result.Operation is null)
            return Fail(result.ErrorCode ?? "GSKU_RETIREMENT_REQUEST_FAILED", result.StatusCode);
        var current = await gskus.GetByIdAsync(r.GskuId, cancellationToken);
        return Response<GskuRetirementRequestWorkflowResult>.Success(new(result.Operation.OperationId,
            result.Operation.GskuId, result.Operation.Checkpoint.ToString(), result.Operation.WorkflowInstanceId,
            current?.Version ?? r.ExpectedGskuVersion, result.IsReplay), result.StatusCode);
    }
    private static Response<GskuRetirementRequestWorkflowResult> Fail(string code, int status) =>
        Response<GskuRetirementRequestWorkflowResult>.Fail(code, status);
}
