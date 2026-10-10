using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Contracts;
using Diten.MdmService.Application.Contracts.Workflow;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Lifecycle;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Handlers.QueryHandlers;
using Diten.MdmService.Application.Features.ProductLegalEntityScopes;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Workflow.Commands;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.StockBehaviour;
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
    LskuIdentityWorkflowExecutionConfiguration executionConfiguration,
    ILskuIdentityWorkflowOperationRepository? identityOperations = null)
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
        // S3b (R-04): a NEW submit of a draft needs its serial flag and its GSKU's lot flag set. S3b FIX1: the repeat of an
        // operation that already exists for this LSKU (the same operation id) is not a new submit and is not stopped here
        // (FIX2: the same maker and expected version, not at a final checkpoint).
        if (lsku.LifecycleStatus == ProductIdentityLifecycleStatus.Draft
            && StockFlagGate.MissingForLsku(lsku, gsku) is { Count: > 0 } missing
            && !await IsRepeatAsync(input, lsku.Id, maker, cancellationToken))
            return Response<LskuIdentityWorkflowResult>.Fail(StockFlagGate.Refusal(missing), 409);
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

    // S3b FIX2 (hardening): a repeat is the SAME submit — this LSKU, this maker, this expected version — still under way
    // (not at a final checkpoint). Anything else under that operation id is a new submit and meets the gate.
    private async Task<bool> IsRepeatAsync(
        StartLskuIdentityWorkflowRequest input, Guid lskuId, Guid maker, CancellationToken cancellationToken) =>
        identityOperations is not null
        && await identityOperations.GetByOperationIdAsync(input.OperationId, cancellationToken) is { } existing
        && existing.LskuId == lskuId
        && existing.MakerSubjectId == maker
        && existing.ExpectedLskuVersion == input.ExpectedVersion
        && existing.Checkpoint is not (LskuIdentityWorkflowCheckpoint.Completed
            or LskuIdentityWorkflowCheckpoint.AbandonedBeforeWorkflowStart
            or LskuIdentityWorkflowCheckpoint.Superseded
            or LskuIdentityWorkflowCheckpoint.WithdrawalApplied
            or LskuIdentityWorkflowCheckpoint.DecisionNotApplicable);

    public const string SubmitAbandoned = "LSKU_IDENTITY_SUBMIT_ABANDONED";
    public const string DecisionNotApplicable = "LSKU_IDENTITY_DECISION_NOT_APPLICABLE";

    private static Response<LskuIdentityWorkflowResult> Fail(string code, int status) =>
        Response<LskuIdentityWorkflowResult>.Fail(code, status);
}
