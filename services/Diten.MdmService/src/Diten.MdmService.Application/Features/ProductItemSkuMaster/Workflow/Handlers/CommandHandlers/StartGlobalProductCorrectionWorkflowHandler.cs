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

public sealed class StartGlobalProductCorrectionWorkflowHandler(
    ITenantContext tenantContext,
    IProductIdentityLifecycleActorContext actorContext,
    IProductIdentityDelegatedTokenAccessor tokenAccessor,
    IGlobalProductRepository products,
    IProductLegalEntityScopeRolloutStateRepository rolloutStates,
    IProductLegalEntityScopePolicyRepository scopePolicies,
    ProductLegalEntityScopeCandidateFacade scopeCandidates,
    GlobalProductCorrectionWorkflowProcessor processor,
    GlobalProductCorrectionExecutionConfiguration execution)
    : IRequestHandler<StartGlobalProductCorrectionWorkflowCommand,
        Response<GlobalProductCorrectionWorkflowResult>>
{
    public async Task<Response<GlobalProductCorrectionWorkflowResult>> Handle(
        StartGlobalProductCorrectionWorkflowCommand command,
        CancellationToken cancellationToken)
    {
        if (!actorContext.TryResolveCanonicalHumanSubject(out var subjectId)
            || !actorContext.HasPermission(ProductIdentityLifecyclePermissions.GlobalProductRequestCorrection))
            return Fail("PRODUCT_IDENTITY_LIFECYCLE_FORBIDDEN", 403);
        string token;
        try { token = tokenAccessor.GetRequiredToken(); }
        catch (ProductIdentityDelegatedTokenException exception) { return Fail(exception.ErrorCode, 401); }
        var request = command.Request;
        var scopeGuard = new ProductLegalEntityScopeConsumerGuard(
            rolloutStates, scopePolicies, scopeCandidates, tenantContext);
        var scope = await scopeGuard.ResolveContextAsync(
            ProductLegalEntityScopeConsumerGuard.GlobalProductReadPermission, cancellationToken);
        if (!scope.IsSuccessful) return Fail(scope.FailureCode!, scope.StatusCode);
        var product = await products.GetByIdAsync(request.GlobalProductId, cancellationToken);
        if (product is null) return Fail("PRODUCT_IDENTITY_NOT_FOUND", 404);
        var scopeDecision = await scopeGuard.EvaluateAsync(scope.Context!, product.Id, cancellationToken);
        if (!scopeDecision.Allowed) return Fail("PRODUCT_IDENTITY_NOT_FOUND", 404);
        var result = await processor.StartInteractiveAsync(tenantContext.TenantId, request.GlobalProductId,
            request.ExpectedVersion, request.OperationId, subjectId, request.GlobalProductName,
            token, execution, cancellationToken);
        if (!result.Succeeded || result.Operation is null)
            return Fail(result.ErrorCode ?? "GLOBAL_PRODUCT_CORRECTION_FAILED", result.StatusCode);
        var current = await products.GetByIdAsync(request.GlobalProductId, cancellationToken);
        return Response<GlobalProductCorrectionWorkflowResult>.Success(new(
            result.Operation.OperationId, result.Operation.GlobalProductId,
            result.Operation.Checkpoint.ToString(), result.Operation.WorkflowInstanceId,
            current?.Version ?? request.ExpectedVersion, result.IsReplay), result.StatusCode);
    }

    private static Response<GlobalProductCorrectionWorkflowResult> Fail(string code, int status) =>
        Response<GlobalProductCorrectionWorkflowResult>.Fail(code, status);
}
