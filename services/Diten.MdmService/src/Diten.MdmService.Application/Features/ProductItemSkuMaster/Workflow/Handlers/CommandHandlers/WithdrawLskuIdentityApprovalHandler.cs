using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Contracts;
using Diten.MdmService.Application.Contracts.Workflow;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Handlers.QueryHandlers;
using Diten.MdmService.Application.Features.ProductLegalEntityScopes;
using Diten.MdmService.Domain.Repositories;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Lifecycle;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Workflow.Commands;
using Diten.MdmService.Domain.Enums;
using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.ProductItemSkuMaster.Workflow.Handlers.CommandHandlers;

public sealed class WithdrawLskuIdentityApprovalHandler(
    ITenantContext tenantContext,
    IProductIdentityLifecycleActorContext actorContext,
    IProductIdentityDelegatedTokenAccessor delegatedTokenAccessor,
    ILskuRepository lskus,
    IGskuRepository gskus,
    IProductDefinitionRevisionRepository revisions,
    IProductLegalEntityScopeRolloutStateRepository rolloutStates,
    IProductLegalEntityScopePolicyRepository policies,
    ProductLegalEntityScopeCandidateFacade candidates,
    LskuIdentityWorkflowProcessor processor,
    LskuIdentityWorkflowExecutionConfiguration executionConfiguration)
    : IRequestHandler<WithdrawLskuIdentityApprovalCommand, Response<LskuIdentityWorkflowResult>>
{
    public async Task<Response<LskuIdentityWorkflowResult>> Handle(
        WithdrawLskuIdentityApprovalCommand command, CancellationToken cancellationToken)
    {
        if (!actorContext.TryResolveCanonicalHumanSubject(out var subject)
            || !actorContext.HasPermission(LskuIdentityLifecyclePermissions.Withdraw))
            return Response<LskuIdentityWorkflowResult>.Fail("LSKU_IDENTITY_LIFECYCLE_FORBIDDEN", 403);
        string token;
        try { token = delegatedTokenAccessor.GetRequiredToken(); }
        catch (ProductIdentityDelegatedTokenException exception)
        { return Response<LskuIdentityWorkflowResult>.Fail(exception.ErrorCode, 401); }
        var input = command.Request;
        if (input is null) return Response<LskuIdentityWorkflowResult>.Fail("LSKU_IDENTITY_WITHDRAWAL_INVALID", 400);
        var guard = new ProductLegalEntityScopeConsumerGuard(rolloutStates, policies, candidates, tenantContext);
        var scope = await guard.ResolveContextAsync(LskuIdentityLifecyclePermissions.Withdraw, cancellationToken);
        if (!scope.IsSuccessful) return Response<LskuIdentityWorkflowResult>.Fail(scope.FailureCode!, scope.StatusCode);
        var lsku = await lskus.GetByIdAsync(input.LskuId, cancellationToken);
        if (lsku is null || lsku.IsDeleted || lsku.TenantId != tenantContext.TenantId)
            return Response<LskuIdentityWorkflowResult>.Fail("LSKU_IDENTITY_NOT_FOUND", 404);
        var gsku = await gskus.GetByIdAsync(lsku.GskuId, cancellationToken);
        var revision = gsku is null ? null : await revisions.GetByIdAsync(gsku.ProductDefinitionRevisionId, cancellationToken);
        if (gsku is null || gsku.IsDeleted || gsku.TenantId != tenantContext.TenantId
            || revision is null || revision.IsDeleted || revision.TenantId != tenantContext.TenantId
            || !(await guard.EvaluateAsync(scope.Context!, revision.GlobalProductId, cancellationToken)).Allowed)
            return Response<LskuIdentityWorkflowResult>.Fail("LSKU_IDENTITY_NOT_FOUND", 404);
        var result = await processor.WithdrawInteractiveAsync(tenantContext.TenantId, input.LskuId,
            input.ExpectedVersion, input.OperationId, subject, input.ReasonCode, input.Comment, token,
            executionConfiguration.LeaseDuration, executionConfiguration.RetryDelay, cancellationToken);
        if (!result.Succeeded || result.Operation is null)
            return Response<LskuIdentityWorkflowResult>.Fail(
                result.ErrorCode ?? "LSKU_IDENTITY_WITHDRAWAL_FAILED", result.StatusCode);
        var operation = result.Operation;
        return Response<LskuIdentityWorkflowResult>.Success(new(operation.OperationId, operation.LskuId,
            operation.Checkpoint.ToString(), operation.WorkflowInstanceId,
            operation.RecoveryDisposition.ToString(), result.IsReplay),
            operation.Checkpoint == LskuIdentityWorkflowCheckpoint.Completed ? 200 : 202);
    }
}
