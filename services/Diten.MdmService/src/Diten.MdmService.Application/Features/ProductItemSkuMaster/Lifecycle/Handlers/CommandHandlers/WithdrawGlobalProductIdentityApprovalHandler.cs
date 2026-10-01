using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Contracts;
using Diten.MdmService.Application.Contracts.Workflow;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Lifecycle.Commands;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Workflow;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Workflow.Commands;
using Diten.MdmService.Application.Features.ProductLegalEntityScopes;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Handlers.QueryHandlers;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.ProductItemSkuMaster.Lifecycle.Handlers.CommandHandlers;

public sealed class WithdrawGlobalProductIdentityApprovalHandler(
    ITenantContext tenantContext,
    IProductIdentityLifecycleActorContext actorContext,
    IProductIdentityDelegatedTokenAccessor delegatedTokenAccessor,
    IGlobalProductRepository products,
    IProductLegalEntityScopeRolloutStateRepository rolloutStates,
    IProductLegalEntityScopePolicyRepository scopePolicies,
    ProductLegalEntityScopeCandidateFacade scopeCandidates,
    GlobalProductIdentityWorkflowProcessor processor,
    ProductIdentityWorkflowExecutionConfiguration executionConfiguration)
    : IRequestHandler<WithdrawGlobalProductIdentityApprovalCommand,
        Response<GlobalProductIdentityWorkflowResult>>
{
    public async Task<Response<GlobalProductIdentityWorkflowResult>> Handle(
        WithdrawGlobalProductIdentityApprovalCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!actorContext.TryResolveCanonicalHumanSubject(out var subjectId)
            || !actorContext.HasPermission(ProductIdentityLifecyclePermissions.GlobalProductWithdraw))
            return Fail("PRODUCT_IDENTITY_LIFECYCLE_FORBIDDEN", 403);

        string token;
        try { token = delegatedTokenAccessor.GetRequiredToken(); }
        catch (ProductIdentityDelegatedTokenException exception) { return Fail(exception.ErrorCode, 401); }

        var input = request.Request;
        var scopeGuard = new ProductLegalEntityScopeConsumerGuard(
            rolloutStates, scopePolicies, scopeCandidates, tenantContext);
        var scope = await scopeGuard.ResolveContextAsync(
            ProductIdentityLifecyclePermissions.GlobalProductWithdraw, cancellationToken);
        if (!scope.IsSuccessful) return Fail(scope.FailureCode!, scope.StatusCode);
        var product = await products.GetByIdAsync(input.GlobalProductId, cancellationToken);
        if (product is null) return Fail("PRODUCT_IDENTITY_NOT_FOUND", 404);
        var scopeDecision = await scopeGuard.EvaluateAsync(scope.Context!, product.Id, cancellationToken);
        if (!scopeDecision.Allowed) return Fail("PRODUCT_IDENTITY_NOT_FOUND", 404);
        var result = await processor.WithdrawInteractiveAsync(tenantContext.TenantId,
            input.GlobalProductId, input.ExpectedVersion, input.OperationId, subjectId,
            input.ReasonCode, input.Comment, token, executionConfiguration.LeaseDuration,
            executionConfiguration.RetryDelay, cancellationToken);
        if (!result.Succeeded || result.Operation is null)
            return Fail(result.ErrorCode ?? "PRODUCT_IDENTITY_WITHDRAWAL_FAILED", result.StatusCode);
        var operation = result.Operation;
        return Response<GlobalProductIdentityWorkflowResult>.Success(new(operation.OperationId,
            operation.GlobalProductId, operation.Checkpoint.ToString(), operation.WorkflowInstanceId,
            operation.RecoveryDisposition.ToString(), result.IsReplay),
            operation.Checkpoint == GlobalProductIdentityWorkflowCheckpoint.Completed ? 200 : 202);
    }

    private static Response<GlobalProductIdentityWorkflowResult> Fail(string code, int status) =>
        Response<GlobalProductIdentityWorkflowResult>.Fail(code, status);
}
