using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Contracts;
using Diten.MdmService.Application.Contracts.Workflow;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Handlers.QueryHandlers;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Lifecycle.Commands;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Workflow;
using Diten.MdmService.Application.Features.ProductLegalEntityScopes;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.ProductItemSkuMaster.Lifecycle.Handlers.CommandHandlers;

public sealed class WithdrawFirstGskuIdentityApprovalHandler(
    ITenantContext tenantContext,
    IProductIdentityLifecycleActorContext actorContext,
    IProductIdentityDelegatedTokenAccessor delegatedTokenAccessor,
    IGskuRepository gskus,
    IProductDefinitionRevisionRepository revisions,
    IGlobalProductRepository products,
    IProductLegalEntityScopeRolloutStateRepository rolloutStates,
    IProductLegalEntityScopePolicyRepository scopePolicies,
    ProductLegalEntityScopeCandidateFacade scopeCandidates,
    FirstGskuIdentityWorkflowProcessor processor,
    FirstGskuIdentityWorkflowExecutionConfiguration executionConfiguration)
    : IRequestHandler<WithdrawFirstGskuIdentityApprovalCommand,
        Response<FirstGskuIdentityLifecycleResult>>
{
    public async Task<Response<FirstGskuIdentityLifecycleResult>> Handle(
        WithdrawFirstGskuIdentityApprovalCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!actorContext.TryResolveCanonicalHumanSubject(out var subjectId)
            || !actorContext.HasPermission(FirstGskuIdentityLifecyclePermissions.Withdraw))
        {
            return Fail("FIRST_GSKU_IDENTITY_LIFECYCLE_FORBIDDEN", 403);
        }

        string token;
        try
        {
            token = delegatedTokenAccessor.GetRequiredToken();
        }
        catch (ProductIdentityDelegatedTokenException exception)
        {
            return Fail(exception.ErrorCode, 401);
        }

        var input = request.Request;
        var scopeGuard = new ProductLegalEntityScopeConsumerGuard(
            rolloutStates, scopePolicies, scopeCandidates, tenantContext);
        var scope = await scopeGuard.ResolveContextAsync(
            FirstGskuIdentityLifecyclePermissions.Withdraw, cancellationToken);
        if (!scope.IsSuccessful) return Fail(scope.FailureCode!, scope.StatusCode);

        var gsku = await gskus.GetByIdAsync(input.GskuId, cancellationToken);
        var revision = gsku is null ? null
            : await revisions.GetByIdAsync(gsku.ProductDefinitionRevisionId, cancellationToken);
        var product = revision is null ? null
            : await products.GetByIdAsync(revision.GlobalProductId, cancellationToken);
        if (gsku is null || revision is null || product is null) return Fail("GSKU_NOT_FOUND", 404);
        var decision = await scopeGuard.EvaluateAsync(scope.Context!, product.Id, cancellationToken);
        if (!decision.Allowed) return Fail("GSKU_NOT_FOUND", 404);

        var result = await processor.WithdrawInteractiveAsync(
            tenantContext.TenantId,
            input.GskuId,
            input.ExpectedGskuVersion,
            input.OperationId,
            subjectId,
            input.ReasonCode,
            input.Comment,
            token,
            executionConfiguration.LeaseDuration,
            executionConfiguration.RetryDelay,
            cancellationToken);
        if (!result.Succeeded || result.Operation is null)
        {
            return Fail(result.ErrorCode ?? "FIRST_GSKU_IDENTITY_WITHDRAWAL_FAILED", result.StatusCode);
        }

        var operation = result.Operation;
        var currentGsku = await gskus.GetByIdAsync(operation.GskuId, cancellationToken);
        var currentRevision = await revisions.GetByIdAsync(
            operation.ProductDefinitionRevisionId, cancellationToken);
        if (currentGsku is null || currentRevision is null)
        {
            return Fail("FIRST_GSKU_IDENTITY_WITHDRAWAL_LOCAL_STATE_INCONSISTENT", 409);
        }

        return Response<FirstGskuIdentityLifecycleResult>.Success(
            new(
                operation.ProductDefinitionRevisionId,
                operation.GskuId,
                currentRevision.LifecycleStatus,
                currentGsku.LifecycleStatus,
                currentRevision.Version,
                currentGsku.Version,
                operation.WorkflowInstanceId,
                result.IsReplay),
            operation.Checkpoint == FirstGskuIdentityWorkflowCheckpoint.Completed ? 200 : 202);
    }

    private static Response<FirstGskuIdentityLifecycleResult> Fail(string code, int status) =>
        Response<FirstGskuIdentityLifecycleResult>.Fail(code, status);
}
