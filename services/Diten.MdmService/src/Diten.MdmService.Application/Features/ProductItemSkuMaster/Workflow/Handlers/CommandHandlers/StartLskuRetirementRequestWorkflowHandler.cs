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

public sealed class StartLskuRetirementRequestWorkflowHandler(ITenantContext tenant,
    IProductIdentityLifecycleActorContext actor, IProductIdentityDelegatedTokenAccessor tokens,
    ILskuRepository lskus, IGskuRepository gskus, IProductDefinitionRevisionRepository revisions,
    IProductLegalEntityScopeRolloutStateRepository rollout, IProductLegalEntityScopePolicyRepository policies,
    ProductLegalEntityScopeCandidateFacade candidates, LskuRetirementRequestWorkflowProcessor processor,
    LskuRetirementRequestExecutionConfiguration execution)
    : IRequestHandler<StartLskuRetirementRequestWorkflowCommand, Response<LskuRetirementRequestWorkflowResult>>
{
    public async Task<Response<LskuRetirementRequestWorkflowResult>> Handle(
        StartLskuRetirementRequestWorkflowCommand command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (!actor.TryResolveCanonicalHumanSubject(out var subjectId)
            || !actor.HasPermission(LskuRetirementRequestPermissions.Request))
            return Fail("PRODUCT_IDENTITY_LIFECYCLE_FORBIDDEN", 403);
        string token;
        try { token = tokens.GetRequiredToken(); }
        catch (ProductIdentityDelegatedTokenException e) { return Fail(e.ErrorCode, 401); }
        var request = command.Request;
        var lsku = await lskus.GetByIdAsync(request.LskuId, ct);
        if (lsku is null) return Fail("LSKU_NOT_FOUND", 404);
        var gsku = await gskus.GetByIdAsync(lsku.GskuId, ct);
        var revision = gsku is null ? null : await revisions.GetByIdAsync(gsku.ProductDefinitionRevisionId, ct);
        if (gsku is null || revision is null) return Fail("LSKU_NOT_FOUND", 404);
        var guard = new ProductLegalEntityScopeConsumerGuard(rollout, policies, candidates, tenant);
        var scope = await guard.ResolveContextAsync(ProductLegalEntityScopeConsumerGuard.GlobalProductReadPermission, ct);
        if (!scope.IsSuccessful) return Fail(scope.FailureCode!, scope.StatusCode);
        if (!(await guard.EvaluateAsync(scope.Context!, revision.GlobalProductId, ct)).Allowed)
            return Fail("LSKU_NOT_FOUND", 404);
        var result = await processor.StartInteractiveAsync(tenant.TenantId, request.LskuId,
            request.ExpectedVersion, request.OperationId, subjectId, request.RequestReason, token,
            execution, ct);
        if (!result.Succeeded || result.Operation is null)
            return Fail(result.ErrorCode ?? "LSKU_RETIREMENT_REQUEST_FAILED", result.StatusCode);
        var current = await lskus.GetByIdAsync(request.LskuId, ct);
        return Response<LskuRetirementRequestWorkflowResult>.Success(new(result.Operation.OperationId,
            result.Operation.LskuId, result.Operation.Checkpoint.ToString(), result.Operation.WorkflowInstanceId,
            current?.Version ?? request.ExpectedVersion, result.IsReplay), result.StatusCode);
    }

    private static Response<LskuRetirementRequestWorkflowResult> Fail(string code, int status) =>
        Response<LskuRetirementRequestWorkflowResult>.Fail(code, status);
}
