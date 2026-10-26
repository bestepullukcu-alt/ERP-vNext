using Diten.MdmService.Application.Features.BrandProductContract;
using Diten.MdmService.Application.Features.Product;
using BrandEntity = Diten.MdmService.Domain.Entities.Brand;
using Diten.MdmService.Domain.Repositories;

namespace Diten.MdmService.Application.Features.ProductItemSkuMaster;

/// <summary>
/// MOD-0290-FU05 — the brand side of a Global Product, in one place for the writes and the reads.
///
/// <para>The link rule is FU02's (<see cref="ProductBrandLink.Refusal"/>), called and never copied: the same refusals and
/// reason codes as the legacy product link (brand_archived 409, brand_not_linkable 409). A brand id that is not one of this
/// tenant's brands — another tenant's included — is brand_not_found 404: a foreign id reads as "no such brand".</para>
///
/// <para>Reads (Karar b): the product stores only the id; the brand's code and name come from <c>mdm_brands</c> when the
/// product is read, one batch read per page (<see cref="IBrandRepository.GetByIdsAsync"/>, tenant-filtered).</para>
/// </summary>
public static class GlobalProductBrands
{
    /// <summary>The brand store was not given to the handler: a write that asks for a brand fails closed.</summary>
    public const string BrandStoreUnavailable = "GLOBAL_PRODUCT_BRAND_STORE_UNAVAILABLE";

    private static readonly IReadOnlyDictionary<Guid, BrandEntity> NoBrands = new Dictionary<Guid, BrandEntity>();

    /// <summary>
    /// Why <paramref name="brandId"/> may not be linked to a Global Product now, or the brand when it may.
    /// </summary>
    public static async Task<(BrandEntity? Brand, (string ReasonCode, string Message, int StatusCode)? Refusal)> ResolveLinkAsync(
        IBrandRepository? brands,
        Guid brandId,
        DateTimeOffset asOf,
        CancellationToken cancellationToken)
    {
        if (brands is null)
        {
            return (null, (BrandStoreUnavailable, "The brand store is not available.", 503));
        }

        var brand = (await brands.GetByIdsAsync([brandId], cancellationToken)).SingleOrDefault();
        if (brand is null)
        {
            return (null, (BrandProductReasonCodes.BrandNotFound, "Brand not found in this tenant.", 404));
        }

        return (brand, ProductBrandLink.Refusal(brand, asOf));
    }

    /// <summary>The brands of a page, by id — one read, whatever the page size.</summary>
    public static async Task<IReadOnlyDictionary<Guid, BrandEntity>> ReadAsync(
        IBrandRepository? brands,
        IEnumerable<Guid?> brandIds,
        CancellationToken cancellationToken)
    {
        var wanted = brandIds.Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToArray();
        if (brands is null || wanted.Length == 0)
        {
            return NoBrands;
        }

        return (await brands.GetByIdsAsync(wanted, cancellationToken)).ToDictionary(brand => brand.Id);
    }

    /// <summary>The read model's three fields: all null without a brand; the id alone when the brand cannot be read.</summary>
    public static (Guid? Id, string? Code, string? Name) Describe(Guid? brandId, IReadOnlyDictionary<Guid, BrandEntity> brands)
        => brandId is { } id
            ? brands.TryGetValue(id, out var brand) ? (id, brand.BrandCode, brand.BrandName) : (id, null, null)
            : (null, null, null);
}
