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

public sealed class StartLskuIdentityWorkflowHandler(
    ITenantContext tenantContext,
    IProductIdentityLifecycleActorContext actorContext,
    IProductIdentityDelegatedTokenAccessor delegatedTokenAccessor,
    ILskuRepository lskus,
    IGskuRepository gskus,
    IProductDefinitionRevisionRepository revisions,
    IGlobalProductRepository products,
    IProductLegalEntityScopeRolloutStateRepository rolloutStates,
    IProductLegalEntityScopePolicyRepository scopePolicies,
    ProductLegalEntityScopeCandidateFacade scopeCandidates,
    LskuIdentityWorkflowProcessor processor,
    LskuIdentityWorkflowExecutionConfiguration executionConfiguration)
    : IRequestHandler<StartLskuIdentityWorkflowCommand, Response<LskuIdentityWorkflowResult>>
{
    public async Task<Response<LskuIdentityWorkflowResult>> Handle(
        StartLskuIdentityWorkflowCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (!actorContext.TryResolveCanonicalHumanSubject(out var maker)
            || !actorContext.HasPermission(LskuIdentityLifecyclePermissions.Submit))
            return Fail("LSKU_IDENTITY_LIFECYCLE_FORBIDDEN", 403);

        string token;
        try { token = delegatedTokenAccessor.GetRequiredToken(); }
        catch (ProductIdentityDelegatedTokenException exception) { return Fail(exception.ErrorCode, 401); }

        var input = command.Request;
        if (input is null) return Fail("LSKU_IDENTITY_WORKFLOW_START_INVALID", 400);
        var scopeGuard = new ProductLegalEntityScopeConsumerGuard(
            rolloutStates, scopePolicies, scopeCandidates, tenantContext);
        var scope = await scopeGuard.ResolveContextAsync(
            LskuIdentityLifecyclePermissions.Submit, cancellationToken);
        if (!scope.IsSuccessful) return Fail(scope.FailureCode!, scope.StatusCode);
        var lsku = await lskus.GetByIdAsync(input.LskuId, cancellationToken);
        var gsku = lsku is null ? null : await gskus.GetByIdAsync(lsku.GskuId, cancellationToken);
        var revision = gsku is null ? null
            : await revisions.GetByIdAsync(gsku.ProductDefinitionRevisionId, cancellationToken);
        var product = revision is null ? null
            : await products.GetByIdAsync(revision.GlobalProductId, cancellationToken);
        if (lsku is null || gsku is null || revision is null || product is null)
            return Fail("LSKU_IDENTITY_NOT_FOUND", 404);
        var decision = await scopeGuard.EvaluateAsync(scope.Context!, product.Id, cancellationToken);
        if (!decision.Allowed) return Fail("LSKU_IDENTITY_NOT_FOUND", 404);
        var result = await processor.StartInteractiveAsync(
            tenantContext.TenantId, input.LskuId, input.ExpectedVersion, input.OperationId,
            maker, token, executionConfiguration.LeaseDuration, executionConfiguration.RetryDelay,
            cancellationToken);
        // LSKU FIX1 B3 / B5: a submit that ended with the LSKU a draft answers a stable code the screen says in the
        // maker's language, and the recorded reason after it.
        if (result.Operation?.Checkpoint is LskuIdentityWorkflowCheckpoint.AbandonedBeforeWorkflowStart
            or LskuIdentityWorkflowCheckpoint.DecisionNotApplicable)
            return Response<LskuIdentityWorkflowResult>.Fail(
                [result.Operation.Checkpoint == LskuIdentityWorkflowCheckpoint.DecisionNotApplicable
                    ? DecisionNotApplicable
                    // FIX2 item 12: the draft changed (cancelled, edited) before any start — not an engine refusal.
                    : result.Operation.LastFailureCode is LskuIdentityWorkflowProcessor.SubmitTargetChanged
                        or LskuIdentityWorkflowProcessor.ClosedByDraftCancel
                        ? LskuIdentityWorkflowProcessor.SubmitTargetChanged
                        : SubmitAbandoned,
                 result.Operation.LastFailureCode ?? result.ErrorCode ?? SubmitAbandoned], 409);
        if (!result.Succeeded || result.Operation is null)
            return Fail(result.ErrorCode ?? "LSKU_IDENTITY_WORKFLOW_START_FAILED", result.StatusCode);

        var operation = result.Operation;
        return Response<LskuIdentityWorkflowResult>.Success(new(
            operation.OperationId, operation.LskuId, operation.Checkpoint.ToString(),
            operation.WorkflowInstanceId, operation.RecoveryDisposition.ToString(), result.IsReplay),
            operation.Checkpoint == LskuIdentityWorkflowCheckpoint.Completed ? 200 : 202);
    }

    public const string SubmitAbandoned = "LSKU_IDENTITY_SUBMIT_ABANDONED";
    public const string DecisionNotApplicable = "LSKU_IDENTITY_DECISION_NOT_APPLICABLE";

    private static Response<LskuIdentityWorkflowResult> Fail(string code, int status) =>
        Response<LskuIdentityWorkflowResult>.Fail(code, status);
}
