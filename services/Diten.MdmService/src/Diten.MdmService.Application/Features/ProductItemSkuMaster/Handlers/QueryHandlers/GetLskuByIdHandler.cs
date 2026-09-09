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

public sealed class GetLskuByIdHandler
    : IRequestHandler<GetLskuByIdQuery, Response<ProductItemSkuMasterModels.LskuDetailDto>>
{
    private readonly ILskuRepository _lskus;
    private readonly IGskuRepository _gskus;
    private readonly IProductDefinitionRevisionRepository _revisions;
    private readonly IGlobalProductRepository _globalProducts;
    private readonly ProductLegalEntityScopeConsumerGuard _scopeGuard;
    private readonly IProductIdentityLifecycleActorContext _actorContext;

    public GetLskuByIdHandler(
        ILskuRepository lskus,
        IGskuRepository gskus,
        IProductDefinitionRevisionRepository revisions,
        IGlobalProductRepository globalProducts,
        IProductLegalEntityScopeRolloutStateRepository rolloutStates,
        IProductLegalEntityScopePolicyRepository policies,
        ProductLegalEntityScopeCandidateFacade candidates,
        ITenantContext tenantContext,
        IProductIdentityLifecycleActorContext? actorContext = null)
    {
        _lskus = lskus;
        _gskus = gskus;
        _revisions = revisions;
        _globalProducts = globalProducts;
        _scopeGuard = new(rolloutStates, policies, candidates, tenantContext);
        _actorContext = actorContext ?? NoActorContext.Instance;
    }

    public async Task<Response<ProductItemSkuMasterModels.LskuDetailDto>> Handle(
        GetLskuByIdQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var scope = await _scopeGuard.ResolveContextAsync("mdm.lskus.read", cancellationToken);
        if (!scope.IsSuccessful)
        {
            return Response<ProductItemSkuMasterModels.LskuDetailDto>.Fail(
                scope.FailureCode!,
                scope.StatusCode);
        }

        var lsku = await _lskus.GetByIdAsync(request.Id, cancellationToken);
        if (lsku is null)
        {
            return Response<ProductItemSkuMasterModels.LskuDetailDto>.Fail("LSKU_NOT_FOUND", 404);
        }

        var gsku = await _gskus.GetByIdAsync(lsku.GskuId, cancellationToken);
        if (gsku is null)
        {
            return Response<ProductItemSkuMasterModels.LskuDetailDto>.Fail(
                "LSKU_NOT_FOUND",
                404);
        }

        var revision = await _revisions.GetByIdAsync(gsku.ProductDefinitionRevisionId, cancellationToken);
        var product = revision is null
            ? null
            : await _globalProducts.GetByIdAsync(revision.GlobalProductId, cancellationToken);
        if (revision is null || product is null)
        {
            return Response<ProductItemSkuMasterModels.LskuDetailDto>.Fail("LSKU_NOT_FOUND", 404);
        }
        var decision = await _scopeGuard.EvaluateAsync(scope.Context!, product.Id, cancellationToken);
        if (!decision.Allowed)
        {
            return Response<ProductItemSkuMasterModels.LskuDetailDto>.Fail("LSKU_NOT_FOUND", 404);
        }

        return Response<ProductItemSkuMasterModels.LskuDetailDto>.Success(new(
            lsku.Id,
            lsku.CanonicalCode,
            lsku.GskuId,
            gsku.CanonicalCode,
            lsku.MarketCode,
            lsku.LifecycleStatus,
            lsku.Version,
            lsku.CreatedAt,
            lsku.UpdatedAt,
            BuildAvailableActions(lsku, _actorContext)));
    }

    internal static IReadOnlyList<string> BuildAvailableActions(
        Lsku lsku,
        IProductIdentityLifecycleActorContext actorContext)
    {
        var actions = new List<string> { "DETAILS" };
        if (lsku.LifecycleStatus == ProductIdentityLifecycleStatus.Draft
            && actorContext.HasPermission(LskuIdentityLifecyclePermissions.Submit))
        {
            actions.Add("SUBMIT");
        }

        if (lsku.LifecycleStatus == ProductIdentityLifecycleStatus.PendingIdentityApproval
            && lsku.IdentityWorkflowBinding is { } binding
            && actorContext.TryResolveCanonicalHumanSubject(out var subjectId)
            && subjectId == binding.SubmitterSubjectId
            && actorContext.HasPermission(LskuIdentityLifecyclePermissions.Withdraw))
        {
            actions.Add("WITHDRAW_APPROVAL");
        }

        if (lsku.LifecycleStatus == ProductIdentityLifecycleStatus.IdentityApproved
            && lsku.ActiveLifecycleOperation is null
            && actorContext.TryResolveCanonicalHumanSubject(out _)
            && actorContext.HasPermission(LskuRetirementRequestPermissions.Request))
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
