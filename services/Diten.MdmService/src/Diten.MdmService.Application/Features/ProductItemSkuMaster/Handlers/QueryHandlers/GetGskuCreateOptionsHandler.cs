using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Contracts;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Lifecycle;
using Diten.MdmService.Application.Contracts.ReferenceData;
using Diten.MdmService.Application.Features.ProductLegalEntityScopes;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Queries;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.ProductItemSkuMaster.Handlers.QueryHandlers;

public sealed class GetGskuMutationOptionsHandler(
    IGskuRepository gskus,
    IProductDefinitionRevisionRepository revisions,
    IGlobalProductRepository products,
    IVerifiedGskuReferenceResolver resolver,
    IProductLegalEntityScopeRolloutStateRepository rolloutStates,
    IProductLegalEntityScopePolicyRepository policies,
    ProductLegalEntityScopeCandidateFacade candidates,
    ITenantContext tenantContext,
    IProductIdentityLifecycleActorContext actorContext)
    : IRequestHandler<GetGskuMutationOptionsQuery, Response<ProductItemSkuMasterModels.GskuMutationOptionsDto>>
{
    public async Task<Response<ProductItemSkuMasterModels.GskuMutationOptionsDto>> Handle(
        GetGskuMutationOptionsQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var permission = request.Operation switch
        {
            GskuMutationOptionsOperation.Edit => FirstGskuIdentityLifecyclePermissions.Update,
            GskuMutationOptionsOperation.Correction => GskuCorrectionPermissions.Request,
            _ => null
        };
        if (request.GskuId == Guid.Empty || permission is null)
            return Response<ProductItemSkuMasterModels.GskuMutationOptionsDto>.Fail("GSKU_OPTIONS_REQUEST_INVALID", 400);
        if (!actorContext.TryResolveCanonicalHumanSubject(out _) || !actorContext.HasPermission(permission))
            return Response<ProductItemSkuMasterModels.GskuMutationOptionsDto>.Fail("GSKU_OPTIONS_FORBIDDEN", 403);

        var guard = new ProductLegalEntityScopeConsumerGuard(rolloutStates, policies, candidates, tenantContext);
        var scope = await guard.ResolveContextAsync(permission, cancellationToken);
        if (!scope.IsSuccessful)
            return Response<ProductItemSkuMasterModels.GskuMutationOptionsDto>.Fail(scope.FailureCode!, scope.StatusCode);

        var gsku = await gskus.GetByIdAsync(request.GskuId, cancellationToken);
        if (gsku is null || gsku.Id != request.GskuId || gsku.IsDeleted || gsku.TenantId != scope.Context!.TenantId)
            return Response<ProductItemSkuMasterModels.GskuMutationOptionsDto>.Fail("GSKU_NOT_FOUND", 404);
        var revision = await revisions.GetByIdAsync(gsku.ProductDefinitionRevisionId, cancellationToken);
        var product = revision is null ? null : await products.GetByIdAsync(revision.GlobalProductId, cancellationToken);
        if (revision is null || revision.IsDeleted || revision.TenantId != gsku.TenantId
            || product is null || product.IsDeleted || product.Id != revision.GlobalProductId
            || product.TenantId != gsku.TenantId
            || !(await guard.EvaluateAsync(scope.Context, product.Id, cancellationToken)).Allowed)
            return Response<ProductItemSkuMasterModels.GskuMutationOptionsDto>.Fail("GSKU_NOT_FOUND", 404);
        var action = request.Operation == GskuMutationOptionsOperation.Edit ? "EDIT" : "REQUEST_CORRECTION";
        if (!GetGskuByIdHandler.BuildPairAvailableActions(gsku, revision, actorContext).Contains(action))
            return Response<ProductItemSkuMasterModels.GskuMutationOptionsDto>.Fail("GSKU_OPTIONS_STATE_CONFLICT", 409);

        var enumeration = await resolver.EnumerateUomsAsync(cancellationToken);
        if (!enumeration.IsSuccessful)
            return Response<ProductItemSkuMasterModels.GskuMutationOptionsDto>.Fail(
                enumeration.FailureCode ?? "REFERENCE_PROVIDER_UNAVAILABLE",
                GskuCreateOptionsFacade.NormalizeProviderStatus(enumeration.StatusCode));
        return Response<ProductItemSkuMasterModels.GskuMutationOptionsDto>.Success(new(
            gsku.Id, gsku.Version, revision.Version,
            enumeration.Uoms.OrderBy(x => x.SortOrder)
                .Select(x => new ProductItemSkuMasterModels.GskuCreateUomOptionDto(
                    x.Code, x.DisplayText, x.SortOrder, x.MaximumDecimalPrecision)).ToList()));
    }
}

public sealed class GetGskuCreateOptionsHandler
    : IRequestHandler<GetGskuCreateOptionsQuery, Response<ProductItemSkuMasterModels.GskuCreateOptionsDto>>
{
    private readonly IGlobalProductRepository _globalProducts;
    private readonly IVerifiedGskuReferenceResolver _resolver;
    private readonly ProductLegalEntityScopeConsumerGuard _scopeGuard;

    public GetGskuCreateOptionsHandler(
        IGlobalProductRepository globalProducts,
        IVerifiedGskuReferenceResolver resolver,
        IProductLegalEntityScopeRolloutStateRepository rolloutStates,
        IProductLegalEntityScopePolicyRepository policies,
        ProductLegalEntityScopeCandidateFacade candidates,
        ITenantContext tenantContext)
    {
        _globalProducts = globalProducts;
        _resolver = resolver;
        _scopeGuard = new(rolloutStates, policies, candidates, tenantContext);
    }

    public async Task<Response<ProductItemSkuMasterModels.GskuCreateOptionsDto>> Handle(
        GetGskuCreateOptionsQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var scope = await _scopeGuard.ResolveContextAsync("mdm.gskus.create", cancellationToken);
        if (!scope.IsSuccessful)
        {
            return Response<ProductItemSkuMasterModels.GskuCreateOptionsDto>.Fail(
                scope.FailureCode!,
                scope.StatusCode);
        }
        if (scope.Context!.RolloutMode == ProductLegalEntityScopeRolloutMode.Preparation)
        {
            return await GskuCreateOptionsFacade.GetAsync(
                _globalProducts,
                _resolver,
                request.PageNumber,
                request.PageSize,
                request.Search,
                cancellationToken);
        }

        var normalizedSearch = string.IsNullOrWhiteSpace(request.Search)
            ? null
            : GlobalProductNameRules.NormalizeDuplicateKey(request.Search);
        var page = scope.Context.RolloutMode == ProductLegalEntityScopeRolloutMode.Enforced
            ? await _globalProducts.GetEnforcedLegalEntityScopePageAsync(
                request.PageNumber,
                request.PageSize,
                normalizedSearch,
                lifecycleStatus: null,
                referenceableOnly: true,
                scope.Context.EffectiveCandidateLegalEntityIds,
                scope.Context.ServerNowUtc,
                cancellationToken)
            : new GlobalProductPage([], 0);
        var enumeration = await _resolver.EnumerateUomsAsync(cancellationToken);
        if (!enumeration.IsSuccessful)
        {
            return Response<ProductItemSkuMasterModels.GskuCreateOptionsDto>.Fail(
                enumeration.FailureCode ?? "REFERENCE_PROVIDER_UNAVAILABLE",
                GskuCreateOptionsFacade.NormalizeProviderStatus(enumeration.StatusCode));
        }

        return Response<ProductItemSkuMasterModels.GskuCreateOptionsDto>.Success(new(
            page.Items.Select(product => new ProductItemSkuMasterModels.GskuCreateGlobalProductOptionDto(
                product.Id,
                product.CanonicalCode,
                product.GlobalProductName)).ToList(),
            enumeration.Uoms
                .OrderBy(item => item.SortOrder)
                .Select(item => new ProductItemSkuMasterModels.GskuCreateUomOptionDto(
                    item.Code,
                    item.DisplayText,
                    item.SortOrder,
                    item.MaximumDecimalPrecision)).ToList()));
    }
}
