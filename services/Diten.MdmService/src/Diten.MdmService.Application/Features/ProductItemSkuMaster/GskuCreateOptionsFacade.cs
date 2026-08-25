using Diten.MdmService.Application.Contracts.ReferenceData;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Repositories;
using Diten.Shared.Core;

namespace Diten.MdmService.Application.Features.ProductItemSkuMaster;

public static class GskuCreateOptionsFacade
{
    public static async Task<Response<ProductItemSkuMasterModels.GskuCreateOptionsDto>> GetAsync(
        IGlobalProductRepository globalProducts,
        IVerifiedGskuReferenceResolver resolver,
        int pageNumber,
        int pageSize,
        string? search,
        CancellationToken cancellationToken)
    {
        var normalizedSearch = string.IsNullOrWhiteSpace(search)
            ? null
            : GlobalProductNameRules.NormalizeDuplicateKey(search);
        var page = await globalProducts.GetReferenceablePageAsync(
            pageNumber,
            pageSize,
            normalizedSearch,
            cancellationToken);
        var enumeration = await resolver.EnumerateUomsAsync(cancellationToken);
        if (!enumeration.IsSuccessful)
        {
            return Response<ProductItemSkuMasterModels.GskuCreateOptionsDto>.Fail(
                enumeration.FailureCode ?? "REFERENCE_PROVIDER_UNAVAILABLE",
                NormalizeProviderStatus(enumeration.StatusCode));
        }

        var products = page.Items.Select(x => new ProductItemSkuMasterModels.GskuCreateGlobalProductOptionDto(
            x.Id,
            x.CanonicalCode,
            x.GlobalProductName)).ToList();
        var uoms = enumeration.Uoms
            .OrderBy(x => x.SortOrder)
            .Select(x => new ProductItemSkuMasterModels.GskuCreateUomOptionDto(
                x.Code,
                x.DisplayText,
                x.SortOrder,
                x.MaximumDecimalPrecision)).ToList();
        return Response<ProductItemSkuMasterModels.GskuCreateOptionsDto>.Success(new(products, uoms));
    }

    internal static int NormalizeProviderStatus(int statusCode) => statusCode switch
    {
        409 => 409,
        504 => 504,
        _ => 503
    };
}
