using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Contracts;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Lifecycle;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Application.Features.ProductLegalEntityScopes;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Queries;
using Diten.MdmService.Domain.Repositories;
using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.ProductItemSkuMaster.Handlers.QueryHandlers;

public sealed class GetGlobalProductByIdHandler : IRequestHandler<GetGlobalProductByIdQuery, Response<ProductItemSkuMasterModels.GlobalProductDetailDto>>
{
    private readonly IGlobalProductRepository _repository;
    private readonly ProductLegalEntityScopeConsumerGuard _scopeGuard;
    private readonly IProductIdentityLifecycleActorContext _actorContext;

    public GetGlobalProductByIdHandler(
        IGlobalProductRepository repository,
        IProductLegalEntityScopeRolloutStateRepository rolloutStates,
        IProductLegalEntityScopePolicyRepository policies,
        ProductLegalEntityScopeCandidateFacade candidates,
        ITenantContext tenantContext,
        IProductIdentityLifecycleActorContext? actorContext = null)
    {
        _repository = repository;
        _scopeGuard = new ProductLegalEntityScopeConsumerGuard(
            rolloutStates,
            policies,
            candidates,
            tenantContext);
        _actorContext = actorContext ?? NoActorContext.Instance;
    }

    public async Task<Response<ProductItemSkuMasterModels.GlobalProductDetailDto>> Handle(
        GetGlobalProductByIdQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var scope = await _scopeGuard.ResolveContextAsync(
            ProductLegalEntityScopeConsumerGuard.GlobalProductReadPermission,
            cancellationToken);
        if (!scope.IsSuccessful)
        {
            return Response<ProductItemSkuMasterModels.GlobalProductDetailDto>.Fail(
                scope.FailureCode!,
                scope.StatusCode);
        }
        var product = await _repository.GetByIdAsync(request.Id, cancellationToken);
        if (product is null)
        {
            return Response<ProductItemSkuMasterModels.GlobalProductDetailDto>.Fail(
                "GLOBAL_PRODUCT_NOT_FOUND",
                404);
        }
        var decision = await _scopeGuard.EvaluateAsync(
            scope.Context!,
            product.Id,
            cancellationToken);
        if (!decision.Allowed)
        {
            return Response<ProductItemSkuMasterModels.GlobalProductDetailDto>.Fail(
                "GLOBAL_PRODUCT_NOT_FOUND",
                404);
        }

        var actions = BuildAvailableActions(product, _actorContext);

        return Response<ProductItemSkuMasterModels.GlobalProductDetailDto>.Success(new(
            product.Id,
            product.CanonicalCode,
            product.GlobalProductName,
            product.LifecycleStatus,
            product.Version,
            product.CreatedAt,
            product.UpdatedAt,
            actions));
    }

    internal static IReadOnlyList<string> BuildAvailableActions(
        Diten.MdmService.Domain.Entities.GlobalProduct product,
        IProductIdentityLifecycleActorContext actorContext)
    {
        var actions = new List<string> { "DETAILS" };
        if (product.LifecycleStatus == ProductIdentityLifecycleStatus.Draft)
        {
            if (actorContext.HasPermission("mdm.global-products.update")) actions.Add("EDIT");
            if (actorContext.HasPermission(ProductIdentityLifecyclePermissions.GlobalProductSubmit))
                actions.Add("SUBMIT");
        }
        if (product.LifecycleStatus == ProductIdentityLifecycleStatus.PendingIdentityApproval
            && product.WorkflowBinding is { } binding
            && actorContext.TryResolveCanonicalHumanSubject(out var subjectId)
            && subjectId == binding.SubmitterSubjectId
            && actorContext.HasPermission(ProductIdentityLifecyclePermissions.GlobalProductWithdraw))
            actions.Add("WITHDRAW_APPROVAL");
        if (product.LifecycleStatus == ProductIdentityLifecycleStatus.IdentityApproved
            && product.ActiveLifecycleOperation is null
            && actorContext.HasPermission(ProductIdentityLifecyclePermissions.GlobalProductRequestCorrection))
            actions.Add("REQUEST_CORRECTION");
        if (product.LifecycleStatus == ProductIdentityLifecycleStatus.IdentityApproved
            && product.ActiveLifecycleOperation is null
            && actorContext.HasPermission(ProductIdentityLifecyclePermissions.GlobalProductRequestRetirement))
            actions.Add("REQUEST_RETIREMENT");
        return actions;
    }

    private sealed class NoActorContext : IProductIdentityLifecycleActorContext
    {
        public static readonly NoActorContext Instance = new();
        public bool TryResolveCanonicalHumanSubject(out Guid subjectId) { subjectId = Guid.Empty; return false; }
        public bool HasPermission(string permission) => false;
    }
}
