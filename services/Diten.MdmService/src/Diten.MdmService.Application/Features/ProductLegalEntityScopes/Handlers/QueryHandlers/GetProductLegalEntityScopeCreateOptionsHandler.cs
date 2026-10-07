using Diten.MdmService.Application.Features.ProductLegalEntityScopes.Queries;
using Diten.MdmService.Domain.Repositories;
using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.ProductLegalEntityScopes.Handlers.QueryHandlers;

public sealed class GetProductLegalEntityScopeCreateOptionsHandler
    : IRequestHandler<GetProductLegalEntityScopeCreateOptionsQuery,
        Response<ProductLegalEntityScopeModels.CreateOptionsDto>>
{
    private const string ModuleCode = "product-item-sku-master";
    private const string ConfigurePermission = "mdm.product-legal-entity-scopes.configure";
    private readonly ProductLegalEntityScopeProductAccess _access;
    private readonly ILegalEntityRepository _legalEntities;
    private readonly ProductLegalEntityScopeCandidateFacade _candidates;

    public GetProductLegalEntityScopeCreateOptionsHandler(
        ProductLegalEntityScopeProductAccess access,
        ILegalEntityRepository legalEntities,
        ProductLegalEntityScopeCandidateFacade candidates)
    {
        _access = access;
        _legalEntities = legalEntities;
        _candidates = candidates;
    }

    public async Task<Response<ProductLegalEntityScopeModels.CreateOptionsDto>> Handle(
        GetProductLegalEntityScopeCreateOptionsQuery request,
        CancellationToken cancellationToken)
    {
        var access = await _access.ResolveAsync(request.GlobalProductId, ConfigurePermission, cancellationToken);
        if (!access.Allowed)
            return Response<ProductLegalEntityScopeModels.CreateOptionsDto>.Fail(access.FailureCode!, access.StatusCode);
        var product = access.Product!;
        // FIX2 (10): the product's code and name are Global Product data — shown only when the caller may also read the
        // product itself (mdm.global-products.read, by their legal entities in an Enforced tenant); else as if missing.
        var productRead = await _access.ResolveAsync(
            request.GlobalProductId,
            Features.ProductItemSkuMaster.Handlers.QueryHandlers.ProductLegalEntityScopeConsumerGuard.GlobalProductReadPermission,
            cancellationToken);
        if (!productRead.Allowed)
            return Response<ProductLegalEntityScopeModels.CreateOptionsDto>.Fail(productRead.FailureCode!, productRead.StatusCode);
        var candidate = await _candidates.ResolveAsync(ModuleCode, ConfigurePermission, cancellationToken);
        if (!candidate.IsSuccessful)
            return Response<ProductLegalEntityScopeModels.CreateOptionsDto>.Fail(
                candidate.FailureCode ?? "LEGAL_ENTITY_SCOPE_PROVIDER_UNAVAILABLE",
                candidate.StatusCode);
        var entities = await _legalEntities.GetReferenceableByIdsAsync(candidate.LegalEntityIds, cancellationToken);
        var options = entities
            .OrderBy(entity => entity.Code, StringComparer.Ordinal)
            .ThenBy(entity => entity.Id)
            .Select(entity => new ProductLegalEntityScopeModels.LegalEntityOptionDto(
                entity.Id, entity.Code, entity.DisplayName ?? entity.LegalName))
            .ToArray();
        return Response<ProductLegalEntityScopeModels.CreateOptionsDto>.Success(new(
            product.Id, product.CanonicalCode, product.GlobalProductName, options));
    }
}
