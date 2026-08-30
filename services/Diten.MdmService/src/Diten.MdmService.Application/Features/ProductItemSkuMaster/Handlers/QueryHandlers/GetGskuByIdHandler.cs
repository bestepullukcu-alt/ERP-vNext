using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Features.ProductLegalEntityScopes;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Queries;
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

    public GetGskuByIdHandler(
        IGskuRepository gskus,
        IProductDefinitionRevisionRepository revisions,
        IGlobalProductRepository globalProducts,
        IProductLegalEntityScopeRolloutStateRepository rolloutStates,
        IProductLegalEntityScopePolicyRepository policies,
        ProductLegalEntityScopeCandidateFacade candidates,
        ITenantContext tenantContext)
    {
        _gskus = gskus;
        _revisions = revisions;
        _globalProducts = globalProducts;
        _scopeGuard = new(rolloutStates, policies, candidates, tenantContext);
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
            gsku.Version,
            gsku.CreatedAt,
            gsku.UpdatedAt));
    }
}
