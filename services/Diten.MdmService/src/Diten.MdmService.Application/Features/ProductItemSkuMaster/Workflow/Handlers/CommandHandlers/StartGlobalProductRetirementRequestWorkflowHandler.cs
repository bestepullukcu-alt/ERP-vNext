using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Contracts;
using Diten.MdmService.Application.Contracts.Workflow;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Handlers.QueryHandlers;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Lifecycle;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Workflow.Commands;
using Diten.MdmService.Application.Features.ProductLegalEntityScopes;
using Diten.MdmService.Domain.Repositories;
using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.ProductItemSkuMaster.Workflow.Handlers.CommandHandlers;

public sealed class StartGlobalProductRetirementRequestWorkflowHandler(
    ITenantContext tenantContext, IProductIdentityLifecycleActorContext actorContext,
    IProductIdentityDelegatedTokenAccessor tokenAccessor, IGlobalProductRepository products,
    IProductLegalEntityScopeRolloutStateRepository rolloutStates,
    IProductLegalEntityScopePolicyRepository scopePolicies,
    ProductLegalEntityScopeCandidateFacade scopeCandidates,
    GlobalProductRetirementRequestWorkflowProcessor processor,
    GlobalProductRetirementRequestExecutionConfiguration execution)
    : IRequestHandler<StartGlobalProductRetirementRequestWorkflowCommand,
        Response<GlobalProductRetirementRequestWorkflowResult>>
{
    public async Task<Response<GlobalProductRetirementRequestWorkflowResult>> Handle(
        StartGlobalProductRetirementRequestWorkflowCommand command, CancellationToken cancellationToken)
    {
        if (!actorContext.TryResolveCanonicalHumanSubject(out var subjectId)
            || !actorContext.HasPermission(ProductIdentityLifecyclePermissions.GlobalProductRequestRetirement))
            return Fail("PRODUCT_IDENTITY_LIFECYCLE_FORBIDDEN", 403);
        string token;
        try { token = tokenAccessor.GetRequiredToken(); }
        catch (ProductIdentityDelegatedTokenException exception) { return Fail(exception.ErrorCode, 401); }
        var request = command.Request;
        var guard = new ProductLegalEntityScopeConsumerGuard(rolloutStates, scopePolicies,
            scopeCandidates, tenantContext);
        var scope = await guard.ResolveContextAsync(
            ProductLegalEntityScopeConsumerGuard.GlobalProductReadPermission, cancellationToken);
        if (!scope.IsSuccessful) return Fail(scope.FailureCode!, scope.StatusCode);
        var product = await products.GetByIdAsync(request.GlobalProductId, cancellationToken);
        if (product is null) return Fail("PRODUCT_IDENTITY_NOT_FOUND", 404);
        if (!(await guard.EvaluateAsync(scope.Context!, product.Id, cancellationToken)).Allowed)
            return Fail("PRODUCT_IDENTITY_NOT_FOUND", 404);
        var result = await processor.StartInteractiveAsync(tenantContext.TenantId, request.GlobalProductId,
            request.ExpectedVersion, request.OperationId, subjectId, request.Reason, token, execution,
            cancellationToken);
        if (!result.Succeeded || result.Operation is null)
            return Fail(result.ErrorCode ?? "GLOBAL_PRODUCT_RETIREMENT_FAILED", result.StatusCode);
        var current = await products.GetByIdAsync(request.GlobalProductId, cancellationToken);
        return Response<GlobalProductRetirementRequestWorkflowResult>.Success(new(result.Operation.OperationId,
            result.Operation.GlobalProductId, result.Operation.Checkpoint.ToString(),
            result.Operation.WorkflowInstanceId, current?.Version ?? request.ExpectedVersion, result.IsReplay),
            result.StatusCode);
    }
    private static Response<GlobalProductRetirementRequestWorkflowResult> Fail(string code, int status) =>
        Response<GlobalProductRetirementRequestWorkflowResult>.Fail(code, status);
}
