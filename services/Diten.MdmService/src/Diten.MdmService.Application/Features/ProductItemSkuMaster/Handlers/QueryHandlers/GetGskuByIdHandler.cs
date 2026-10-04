using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Contracts;
using Diten.MdmService.Application.Features.ProductLegalEntityScopes;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Lifecycle;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Queries;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
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

    public GetGskuByIdHandler(
        IGskuRepository gskus,
        IProductDefinitionRevisionRepository revisions,
        IGlobalProductRepository globalProducts,
        IProductLegalEntityScopeRolloutStateRepository rolloutStates,
        IProductLegalEntityScopePolicyRepository policies,
        ProductLegalEntityScopeCandidateFacade candidates,
        ITenantContext tenantContext,
        IProductIdentityLifecycleActorContext? actorContext = null,
        IFirstGskuIdentityWorkflowOperationRepository? identityOperations = null)
    {
        _identityOperations = identityOperations;
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
        var submit = gsku.LifecycleStatus == ProductIdentityLifecycleStatus.Draft && _identityOperations is not null
            ? await _identityOperations.GetSubmitStateAsync(gsku.Id, gsku.Version, cancellationToken)
            : FirstGskuIdentitySubmitState.None;
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
            BuildPairAvailableActions(gsku, revision, _actorContext, submit.Kind),
            gsku.LifecycleStatus == ProductIdentityLifecycleStatus.Draft && _identityOperations is not null
                ? await _identityOperations.LatestNotApplicableReasonAsync(gsku.Id, cancellationToken)
                : null,
            SubmitStateName(submit.Kind)));
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
        FirstGskuIdentitySubmitStateKind submit = FirstGskuIdentitySubmitStateKind.None)
    {
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
        FirstGskuIdentitySubmitStateKind submit = FirstGskuIdentitySubmitStateKind.None)
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
            if (submit is FirstGskuIdentitySubmitStateKind.None or FirstGskuIdentitySubmitStateKind.ResubmitRequired
                    or FirstGskuIdentitySubmitStateKind.ReconciliationRequired
                && actorContext.HasPermission(FirstGskuIdentityLifecyclePermissions.Submit)) actions.Add("SUBMIT");
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
