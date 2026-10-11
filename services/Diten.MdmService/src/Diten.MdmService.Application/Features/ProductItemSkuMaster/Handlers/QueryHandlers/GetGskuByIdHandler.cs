using Diten.MdmService.Application.Features.ProductItemSkuMaster.StockBehaviour;
using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Contracts;
using Diten.MdmService.Application.Features.ProductLegalEntityScopes;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Lifecycle;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Queries;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using Diten.MdmService.Domain.ValueObjects;
using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.ProductItemSkuMaster.Handlers.QueryHandlers;

public sealed class GetGskuByIdHandler
    : IRequestHandler<GetGskuByIdQuery, Response<ProductItemSkuMasterModels.GskuDetailDto>>
{
    private readonly IGskuRepository _gskus;
    private readonly IProductDefinitionRevisionRepository _revisions;
    private readonly IGlobalProductRepository _globalProducts;
    private readonly ProductLegalEntityScopeConsumerGuard _scopeGuard;
    private readonly IProductIdentityLifecycleActorContext _actorContext;
    private readonly IFirstGskuIdentityWorkflowOperationRepository? _identityOperations;
    private readonly IGskuCorrectionWorkflowOperationRepository? _corrections;
    private readonly IGskuRetirementRequestOperationRepository? _retirementRequests;
    // S5-FIX1 (1): the stock write's own "held by an operation" predicates, for the section's editable flag.
    private readonly IProductStockBehaviourRepository _stockBehaviour;

    public GetGskuByIdHandler(
        IGskuRepository gskus,
        IProductDefinitionRevisionRepository revisions,
        IGlobalProductRepository globalProducts,
        IProductLegalEntityScopeRolloutStateRepository rolloutStates,
        IProductLegalEntityScopePolicyRepository policies,
        ProductLegalEntityScopeCandidateFacade candidates,
        ITenantContext tenantContext,
        // S5b-FIX2: required — the detail's editable flag always asks the write's own predicates.
        IProductStockBehaviourRepository stockBehaviour,
        IProductIdentityLifecycleActorContext? actorContext = null,
        IFirstGskuIdentityWorkflowOperationRepository? identityOperations = null,
        IGskuCorrectionWorkflowOperationRepository? corrections = null,
        IGskuRetirementRequestOperationRepository? retirementRequests = null)
    {
        _stockBehaviour = stockBehaviour;
        _identityOperations = identityOperations;
        _corrections = corrections;
        _retirementRequests = retirementRequests;
        _gskus = gskus;
        _revisions = revisions;
        _globalProducts = globalProducts;
        _scopeGuard = new(rolloutStates, policies, candidates, tenantContext);
        _actorContext = actorContext ?? NoActorContext.Instance;
    }

    public async Task<Response<ProductItemSkuMasterModels.GskuDetailDto>> Handle(
        GetGskuByIdQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var scope = await _scopeGuard.ResolveContextAsync("mdm.gskus.read", cancellationToken);
        if (!scope.IsSuccessful)
        {
            return Response<ProductItemSkuMasterModels.GskuDetailDto>.Fail(
                scope.FailureCode!,
                scope.StatusCode);
        }

        var gsku = await _gskus.GetByIdAsync(request.Id, cancellationToken);
        if (gsku is null)
        {
            return Response<ProductItemSkuMasterModels.GskuDetailDto>.Fail("GSKU_NOT_FOUND", 404);
        }

        var revision = await _revisions.GetByIdAsync(gsku.ProductDefinitionRevisionId, cancellationToken);
        var product = revision is null
            ? null
            : await _globalProducts.GetByIdAsync(revision.GlobalProductId, cancellationToken);
        if (revision is null || product is null)
        {
            return Response<ProductItemSkuMasterModels.GskuDetailDto>.Fail("GSKU_NOT_FOUND", 404);
        }

        var decision = await _scopeGuard.EvaluateAsync(scope.Context!, product.Id, cancellationToken);
        if (!decision.Allowed)
        {
            return Response<ProductItemSkuMasterModels.GskuDetailDto>.Fail("GSKU_NOT_FOUND", 404);
        }

        // FIX2 item 1: where the draft's latest submit stands decides its actions and the sentence on the screen.
        // FIX3 E2: a pending pair whose submit is in manual reconciliation has a way out too (the reconciling repeat).
        var submit = gsku.LifecycleStatus is ProductIdentityLifecycleStatus.Draft
                         or ProductIdentityLifecycleStatus.PendingIdentityApproval && _identityOperations is not null
            ? await _identityOperations!.GetSubmitStateAsync(gsku.Id, gsku.Version, cancellationToken)
            : FirstGskuIdentitySubmitState.None;
        if (gsku.LifecycleStatus == ProductIdentityLifecycleStatus.PendingIdentityApproval
            && submit.Kind != FirstGskuIdentitySubmitStateKind.ReconciliationRequired)
            submit = FirstGskuIdentitySubmitState.None;
        var stuckRequest = await StuckLifecycleRequestAsync(gsku, cancellationToken);
        var actions = BuildPairAvailableActions(gsku, revision, _actorContext, submit.Kind, submit.InWithdrawal,
            submit.SubmitterSubjectId);
        if (stuckRequest is { Requester: true }) actions = [.. actions, "REPEAT_REQUEST"];
        return Response<ProductItemSkuMasterModels.GskuDetailDto>.Success(new(
            gsku.Id,
            gsku.CanonicalCode,
            product.Id,
            product.CanonicalCode,
            product.GlobalProductName,
            revision.Id,
            revision.RevisionIdentifier,
            gsku.PackQuantity,
            gsku.PackUomCode,
            gsku.LifecycleStatus,
            revision.Version,
            gsku.Version,
            gsku.CreatedAt,
            gsku.UpdatedAt,
            actions,
            gsku.LifecycleStatus == ProductIdentityLifecycleStatus.Draft && _identityOperations is not null
                ? await _identityOperations.LatestNotApplicableReasonAsync(gsku.Id, cancellationToken)
                : null,
            SubmitStateName(submit.Kind),
            submit.Kind == FirstGskuIdentitySubmitStateKind.ReconciliationRequired ? submit.SubmittedAtVersion : null,
            gsku.LifecycleStatus == ProductIdentityLifecycleStatus.IdentityApproved && _identityOperations is not null
                ? await _identityOperations.LatestApprovalNoteAsync(gsku.Id, cancellationToken)
                : null,
            stuckRequest is null ? null : "STUCK",
            stuckRequest is { Requester: true } ? stuckRequest.Request : null)
        {
            StockBehaviour = global::Diten.MdmService.Application.Features.ProductItemSkuMaster.StockBehaviour.StockBehaviourView.ForGsku(gsku,
                await _stockBehaviour.GskuHeldAsync(gsku, cancellationToken))
        });
    }

    /*
     * FIX4 item 1 (b): a correction or retirement request STUCK in manual reconciliation holds the GSKU's lock. Everyone
     * sees that (LifecycleRequestState STUCK); its requester is offered REPEAT_REQUEST — the same command again (same
     * version and facts, so the same operation), which reads the engine and settles it.
     */
    private sealed record StuckRequest(bool Requester, ProductItemSkuMasterModels.GskuPendingLifecycleRequestDto Request);

    private async Task<StuckRequest?> StuckLifecycleRequestAsync(Gsku gsku, CancellationToken cancellationToken)
    {
        if (gsku.ActiveLifecycleOperation is not { } held) return null;
        _actorContext.TryResolveCanonicalHumanSubject(out var subject);
        if (held.Kind == GskuLifecycleOperationKind.Correction && _corrections is not null
            && await _corrections.GetByOperationIdAsync(held.OperationId, cancellationToken) is { } correction
            && correction.Checkpoint == GskuCorrectionWorkflowCheckpoint.ManualReconciliationRequired
            && correction.RecoveryDisposition == ProductIdentityWorkflowRecoveryDisposition.ManualReconciliationRequired)
            return new(subject == correction.MakerSubjectId && _actorContext.HasPermission(GskuCorrectionPermissions.Request),
                new("CORRECTION", correction.BaseGskuVersion, correction.ProposedPackQuantity,
                    correction.ProposedPackUomCode, null));
        if (held.Kind == GskuLifecycleOperationKind.Retirement && _retirementRequests is not null
            && await _retirementRequests.GetByOperationIdAsync(held.OperationId, cancellationToken) is { } retirement
            && retirement.Checkpoint == GskuRetirementRequestCheckpoint.ManualReconciliationRequired
            && retirement.RecoveryDisposition == ProductIdentityWorkflowRecoveryDisposition.ManualReconciliationRequired)
            return new(subject == retirement.MakerSubjectId && _actorContext.HasPermission(GskuRetirementRequestPermissions.Request),
                new("RETIREMENT", retirement.BaseGskuVersion, null, null, retirement.RequestReason));
        return null;
    }

    internal static string? SubmitStateName(FirstGskuIdentitySubmitStateKind kind) => kind switch
    {
        FirstGskuIdentitySubmitStateKind.InFlight => "SUBMIT_IN_FLIGHT",
        FirstGskuIdentitySubmitStateKind.ResubmitRequired => "RESUBMIT_REQUIRED",
        FirstGskuIdentitySubmitStateKind.ReconciliationRequired => "RECONCILIATION_REQUIRED",
        FirstGskuIdentitySubmitStateKind.Ended => "SUBMISSION_ENDED",
        _ => null
    };

    internal static IReadOnlyList<string> BuildPairAvailableActions(
        Gsku gsku, ProductDefinitionRevision revision, IProductIdentityLifecycleActorContext actorContext,
        FirstGskuIdentitySubmitStateKind submit = FirstGskuIdentitySubmitStateKind.None, bool inWithdrawal = false,
        Guid? submitter = null)
    {
        // FIX3 E2: a submit in manual reconciliation may have left the pair half-written (the revision pending, the
        // GSKU not yet): the pair looks inconsistent, and the reconciling repeat is exactly its way out.
        if (submit == FirstGskuIdentitySubmitStateKind.ReconciliationRequired && !inWithdrawal
            && !gsku.IsDeleted && gsku.ActiveLifecycleOperation is null && !gsku.RetirementOperationId.HasValue
            && gsku.LifecycleStatus is ProductIdentityLifecycleStatus.Draft or ProductIdentityLifecycleStatus.PendingIdentityApproval
            && actorContext.TryResolveCanonicalHumanSubject(out var subject)
            && actorContext.HasPermission(FirstGskuIdentityLifecyclePermissions.Submit))
        {
            var actions = BuildAvailableActions(gsku, actorContext, submit, submitter).ToList();
            // FIX4 item 11: only the submitter's repeat reconciles (the engine reads it with their token).
            if (subject == submitter && !actions.Contains("SUBMIT")) actions.Add("SUBMIT");
            return actions;
        }
        if (gsku.ProductDefinitionRevisionId != revision.Id || gsku.TenantId != revision.TenantId
            || revision.IsDeleted || gsku.LifecycleStatus != revision.LifecycleStatus
            || revision.RetirementOperationId.HasValue
            || !string.Equals(System.Text.Json.JsonSerializer.Serialize(gsku.IdentityWorkflowBinding),
                System.Text.Json.JsonSerializer.Serialize(revision.IdentityWorkflowBinding), StringComparison.Ordinal))
            return ["DETAILS"];
        return BuildAvailableActions(gsku, actorContext, submit);
    }

    internal static IReadOnlyList<string> BuildAvailableActions(
        Gsku gsku,
        IProductIdentityLifecycleActorContext actorContext,
        FirstGskuIdentitySubmitStateKind submit = FirstGskuIdentitySubmitStateKind.None,
        Guid? submitter = null)
    {
        var actions = new List<string> { "DETAILS" };
        if (gsku.IsDeleted || gsku.ActiveLifecycleOperation is not null || gsku.RetirementOperationId.HasValue
            || !actorContext.TryResolveCanonicalHumanSubject(out _))
            return actions;
        // FIX2 item 1 — every draft state has a way out:
        //   no submit / never received by the engine → edit, or submit (the same submit again);
        //   on its way                               → neither (B7); the worker carries it on;
        //   the engine may hold it                   → submit only: the submitter's repeat reads the engine and reconciles;
        //   ended at this version                    → edit only: the same submit cannot be repeated, an edited one can.
        if (gsku.LifecycleStatus == ProductIdentityLifecycleStatus.Draft)
        {
            if (submit is FirstGskuIdentitySubmitStateKind.None or FirstGskuIdentitySubmitStateKind.ResubmitRequired
                    or FirstGskuIdentitySubmitStateKind.Ended
                && actorContext.HasPermission(FirstGskuIdentityLifecyclePermissions.Update)) actions.Add("EDIT");
            // FIX4 item 11: a submit in reconciliation is offered to its submitter only (only their repeat settles it);
            // others see the state and a sentence saying who can.
            if ((submit is FirstGskuIdentitySubmitStateKind.None or FirstGskuIdentitySubmitStateKind.ResubmitRequired
                 || submit == FirstGskuIdentitySubmitStateKind.ReconciliationRequired
                    && actorContext.TryResolveCanonicalHumanSubject(out var submitterSubject) && submitterSubject == submitter)
                && actorContext.HasPermission(FirstGskuIdentityLifecyclePermissions.Submit))
                // S3b FIX1 (R2): a NEW submit the gate would refuse is not offered — its reason is (the reconciling repeat
                // of an existing operation is not a new submit and stays offered).
                actions.Add(submit != FirstGskuIdentitySubmitStateKind.ReconciliationRequired
                            && StockFlagGate.SubmitRefusalForGsku(gsku) is not null
                    ? StockFlagGate.SubmitNeedsStockFlagsAction
                    : "SUBMIT");
        }

        if (gsku.LifecycleStatus == ProductIdentityLifecycleStatus.PendingIdentityApproval
            && gsku.IdentityWorkflowBinding is { } binding
            && actorContext.TryResolveCanonicalHumanSubject(out var subjectId)
            && subjectId == binding.SubmitterSubjectId
            && actorContext.HasPermission(FirstGskuIdentityLifecyclePermissions.Withdraw))
        {
            actions.Add("WITHDRAW_APPROVAL");
        }

        if (gsku.LifecycleStatus == ProductIdentityLifecycleStatus.IdentityApproved
            && gsku.ActiveLifecycleOperation is null
            && actorContext.TryResolveCanonicalHumanSubject(out _)
            && actorContext.HasPermission(GskuCorrectionPermissions.Request))
        {
            actions.Add("REQUEST_CORRECTION");
        }

        if (gsku.LifecycleStatus == ProductIdentityLifecycleStatus.IdentityApproved
            && gsku.ActiveLifecycleOperation is null
            && actorContext.TryResolveCanonicalHumanSubject(out _)
            && actorContext.HasPermission(GskuRetirementRequestPermissions.Request))
        {
            actions.Add("REQUEST_RETIREMENT");
        }

        return actions;
    }

    private sealed class NoActorContext : IProductIdentityLifecycleActorContext
    {
        public static readonly NoActorContext Instance = new();
        public bool TryResolveCanonicalHumanSubject(out Guid subjectId)
        {
            subjectId = Guid.Empty;
            return false;
        }

        public bool HasPermission(string permission) => false;
    }
}
