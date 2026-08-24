using Diten.MdmService.Application.Contracts.ReferenceData;
using Diten.MdmService.Domain.Repositories;
using Diten.Shared.Core;

namespace Diten.MdmService.Application.Features.ProductItemSkuMaster;

public static class LskuCreateOptionsFacade
{
    public static async Task<Response<ProductItemSkuMasterModels.LskuCreateOptionsDto>> GetAsync(
        IGskuRepository gskus,
        IProductDefinitionRevisionRepository revisions,
        IGlobalProductRepository globalProducts,
        IVerifiedMarketReferenceResolver markets,
        int pageNumber,
        int pageSize,
        string? search,
        CancellationToken cancellationToken)
    {
        var normalizedSearch = string.IsNullOrWhiteSpace(search)
            ? null
            : search.Trim().ToUpperInvariant();
        var page = await gskus.GetReferenceablePageAsync(
            pageNumber,
            pageSize,
            normalizedSearch,
            cancellationToken);
        var revisionItems = await revisions.GetByIdsAsync(
            page.Items.Select(x => x.ProductDefinitionRevisionId).Distinct().ToArray(),
            cancellationToken);
        var revisionById = revisionItems.ToDictionary(x => x.Id);
        if (page.Items.Any(x => !revisionById.ContainsKey(x.ProductDefinitionRevisionId)))
        {
            return Fail("GSKU_PARENT_BINDING_INVARIANT_VIOLATION", 409);
        }

        var productItems = await globalProducts.GetByIdsAsync(
            revisionItems.Select(x => x.GlobalProductId).Distinct().ToArray(),
            cancellationToken);
        var productById = productItems.ToDictionary(x => x.Id);
        if (revisionItems.Any(x => !productById.ContainsKey(x.GlobalProductId)))
        {
            return Fail("GSKU_PARENT_BINDING_INVARIANT_VIOLATION", 409);
        }

        var enumeration = await markets.EnumerateActiveAsync(cancellationToken);
        if (!enumeration.IsSuccessful)
        {
            return Fail(
                enumeration.FailureCode ?? "REFERENCE_PROVIDER_UNAVAILABLE",
                NormalizeProviderStatus(enumeration.StatusCode));
        }

        var gskuOptions = page.Items.Select(gsku =>
        {
            var revision = revisionById[gsku.ProductDefinitionRevisionId];
            var product = productById[revision.GlobalProductId];
            return new ProductItemSkuMasterModels.LskuCreateGskuOptionDto(
                gsku.Id,
                gsku.CanonicalCode,
                product.CanonicalCode,
                product.GlobalProductName,
                revision.RevisionIdentifier,
                gsku.PackQuantity,
                gsku.PackUomCode);
        }).ToList();
        var marketOptions = enumeration.Markets
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Code, StringComparer.Ordinal)
            .Select(x => new ProductItemSkuMasterModels.LskuCreateMarketOptionDto(
                x.Code,
                x.DisplayText,
                x.SortOrder))
            .ToList();
        return Response<ProductItemSkuMasterModels.LskuCreateOptionsDto>.Success(new(gskuOptions, marketOptions));
    }

    internal static int NormalizeProviderStatus(int statusCode) => statusCode switch
    {
        404 => 404,
        409 => 409,
        504 => 504,
        _ => 503
    };

    private static Response<ProductItemSkuMasterModels.LskuCreateOptionsDto> Fail(
        string code,
        int statusCode) =>
        Response<ProductItemSkuMasterModels.LskuCreateOptionsDto>.Fail(code, statusCode);
}
