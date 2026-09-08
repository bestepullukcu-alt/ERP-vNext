using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Contracts;
using Diten.MdmService.Application.Contracts.Workflow;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Lifecycle;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Handlers.QueryHandlers;
using Diten.MdmService.Application.Features.ProductLegalEntityScopes;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Workflow.Commands;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.ProductItemSkuMaster.Workflow.Handlers.CommandHandlers;

public sealed class StartFirstGskuIdentityWorkflowHandler(
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
    : IRequestHandler<StartFirstGskuIdentityWorkflowCommand, Response<FirstGskuIdentityWorkflowResult>>
{
    public async Task<Response<FirstGskuIdentityWorkflowResult>> Handle(
        StartFirstGskuIdentityWorkflowCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!actorContext.TryResolveCanonicalHumanSubject(out var makerSubjectId)
            || !actorContext.HasPermission(FirstGskuIdentityLifecyclePermissions.Submit))
        {
            return Fail("FIRST_GSKU_IDENTITY_LIFECYCLE_FORBIDDEN", 403);
        }

        string delegatedToken;
        try
        {
            delegatedToken = delegatedTokenAccessor.GetRequiredToken();
        }
        catch (ProductIdentityDelegatedTokenException exception)
        {
            return Fail(exception.ErrorCode, 401);
        }

        var input = request.Request;
        if (input is null)
        {
            return Fail("FIRST_GSKU_IDENTITY_WORKFLOW_START_INVALID", 400);
        }
        var scopeGuard = new ProductLegalEntityScopeConsumerGuard(
            rolloutStates, scopePolicies, scopeCandidates, tenantContext);
        var scope = await scopeGuard.ResolveContextAsync(
            FirstGskuIdentityLifecyclePermissions.Submit, cancellationToken);
        if (!scope.IsSuccessful) return Fail(scope.FailureCode!, scope.StatusCode);
        var gsku = await gskus.GetByIdAsync(input.GskuId, cancellationToken);
        var revision = gsku is null ? null
            : await revisions.GetByIdAsync(gsku.ProductDefinitionRevisionId, cancellationToken);
        var product = revision is null ? null
            : await products.GetByIdAsync(revision.GlobalProductId, cancellationToken);
        if (gsku is null || revision is null || product is null) return Fail("GSKU_NOT_FOUND", 404);
        var decision = await scopeGuard.EvaluateAsync(scope.Context!, product.Id, cancellationToken);
        if (!decision.Allowed) return Fail("GSKU_NOT_FOUND", 404);
        var result = await processor.StartInteractiveAsync(
            tenantContext.TenantId,
            input.GskuId,
            input.ExpectedGskuVersion,
            input.OperationId,
            makerSubjectId,
            delegatedToken,
            executionConfiguration.LeaseDuration,
            executionConfiguration.RetryDelay,
            cancellationToken);
        if (!result.Succeeded || result.Operation is null)
        {
            return Fail(result.ErrorCode ?? "FIRST_GSKU_IDENTITY_WORKFLOW_START_FAILED", result.StatusCode);
        }

        var operation = result.Operation;
        return Response<FirstGskuIdentityWorkflowResult>.Success(
            new(
                operation.OperationId,
                operation.ProductDefinitionRevisionId,
                operation.GskuId,
                operation.Checkpoint.ToString(),
                operation.WorkflowInstanceId,
                operation.RecoveryDisposition.ToString(),
                result.IsReplay),
            operation.Checkpoint == FirstGskuIdentityWorkflowCheckpoint.Completed ? 200 : 202);
    }

    private static Response<FirstGskuIdentityWorkflowResult> Fail(string code, int statusCode) =>
        Response<FirstGskuIdentityWorkflowResult>.Fail(code, statusCode);
}
