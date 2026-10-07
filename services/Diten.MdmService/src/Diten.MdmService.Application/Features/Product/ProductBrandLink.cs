using Diten.MdmService.Application.Features.BrandProductContract;

namespace Diten.MdmService.Application.Features.Product;

/// <summary>
/// WP-MDM-BRAND-ACCEPT-01 FIX2 (item 2) — why a NEW product link to <c>brand</c> is refused, or null when it is allowed.
/// Archived keeps its own code (<see cref="BrandProductReasonCodes.BrandArchived"/>); draft, inactive and a brand outside
/// its validity window are <see cref="BrandProductReasonCodes.BrandNotLinkable"/>.
/// </summary>
public static class ProductBrandLink
{
    public static (string ReasonCode, string Message, int StatusCode)? Refusal(Domain.Entities.Brand brand, DateTimeOffset asOf)
    {
        if (brand.IsArchived)
        {
            return (BrandProductReasonCodes.BrandArchived, "An archived brand cannot receive new product links.", 409);
        }

        return brand.IsLinkableAt(asOf)
            ? null
            : (BrandProductReasonCodes.BrandNotLinkable,
                "Only an active brand inside its validity window can receive new product links.", 409);
    }
}
