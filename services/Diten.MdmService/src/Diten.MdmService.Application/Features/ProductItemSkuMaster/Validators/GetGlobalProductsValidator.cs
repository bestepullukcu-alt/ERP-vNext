using Diten.MdmService.Application.Features.ProductItemSkuMaster.Queries;
using FluentValidation;

namespace Diten.MdmService.Application.Features.ProductItemSkuMaster.Validators;

public sealed class GetGlobalProductsValidator : AbstractValidator<GetGlobalProductsQuery>
{
    public GetGlobalProductsValidator()
    {
        RuleFor(x => x.PageNumber).InclusiveBetween(1, 1_000_000);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
        RuleFor(x => x.Search).MaximumLength(200);
        RuleFor(x => x.LifecycleStatus).IsInEnum().When(x => x.LifecycleStatus.HasValue);
        RuleFor(x => x.BrandId)
            .Must(brandId => brandId != Guid.Empty)
            .When(x => x.BrandId.HasValue)
            .WithMessage("GLOBAL_PRODUCT_LIST_BRAND_ID_INVALID");
        // WP-MDM-GP-LIST-01: the sort key comes from the whitelist, never from the request as typed.
        RuleFor(x => x.OrderBy)
            .Must(orderBy => GetGlobalProductsQuery.SortableFields.ContainsKey(orderBy!))
            .When(x => !string.IsNullOrEmpty(x.OrderBy))
            .WithMessage("GLOBAL_PRODUCT_LIST_ORDER_BY_INVALID");
        RuleFor(x => x.OrderDir)
            .Must(GetGlobalProductsQuery.IsKnownOrderDir)
            .WithMessage("GLOBAL_PRODUCT_LIST_ORDER_DIR_INVALID");
    }
}
