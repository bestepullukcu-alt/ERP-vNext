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
    private readonly IGlobalProductRepository _products;
    private readonly ILegalEntityRepository _legalEntities;
    private readonly ProductLegalEntityScopeCandidateFacade _candidates;

    public GetProductLegalEntityScopeCreateOptionsHandler(
        IGlobalProductRepository products,
        ILegalEntityRepository legalEntities,
        ProductLegalEntityScopeCandidateFacade candidates)
    {
        _products = products;
        _legalEntities = legalEntities;
        _candidates = candidates;
    }

    public async Task<Response<ProductLegalEntityScopeModels.CreateOptionsDto>> Handle(
        GetProductLegalEntityScopeCreateOptionsQuery request,
        CancellationToken cancellationToken)
    {
        var product = await _products.GetByIdAsync(request.GlobalProductId, cancellationToken);
        if (product is null)
            return Response<ProductLegalEntityScopeModels.CreateOptionsDto>.Fail("GLOBAL_PRODUCT_NOT_FOUND", 404);
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
