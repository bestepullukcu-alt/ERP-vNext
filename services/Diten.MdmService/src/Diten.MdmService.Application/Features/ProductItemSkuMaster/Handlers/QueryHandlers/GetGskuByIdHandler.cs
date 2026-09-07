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

    public GetGskuByIdHandler(
        IGskuRepository gskus,
        IProductDefinitionRevisionRepository revisions,
        IGlobalProductRepository globalProducts,
        IProductLegalEntityScopeRolloutStateRepository rolloutStates,
        IProductLegalEntityScopePolicyRepository policies,
        ProductLegalEntityScopeCandidateFacade candidates,
        ITenantContext tenantContext,
        IProductIdentityLifecycleActorContext? actorContext = null)
    {
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
            BuildAvailableActions(gsku, _actorContext)));
    }

    internal static IReadOnlyList<string> BuildAvailableActions(
        Gsku gsku,
        IProductIdentityLifecycleActorContext actorContext)
    {
        var actions = new List<string> { "DETAILS" };
        if (gsku.LifecycleStatus == ProductIdentityLifecycleStatus.Draft)
        {
            if (actorContext.HasPermission(FirstGskuIdentityLifecyclePermissions.Update)) actions.Add("EDIT");
            if (actorContext.HasPermission(FirstGskuIdentityLifecyclePermissions.Submit)) actions.Add("SUBMIT");
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
